using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SPRMS.Common;

namespace SPRMS.API.Common.Middleware;

// ============================================================
// 1. EXCEPTION MIDDLEWARE
// ============================================================
public sealed class ExceptionMiddleware(RequestDelegate next, ILogChannel log, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception TraceId:{t}", ctx.TraceIdentifier);
            log.WriteError(new ErrorLogWrite(
                Module: "Global", FunctionName: ctx.GetEndpoint()?.DisplayName ?? "unknown",
                Message: ex.Message, StackTrace: ex.StackTrace, InnerException: ex.InnerException?.Message,
                RequestPath: ctx.Request.Path, HTTPMethod: ctx.Request.Method, HttpStatus: 500,
                RequestID: ctx.TraceIdentifier, UserID: GetUid(ctx), IPAddress: GetIP(ctx),
                MachineName: Environment.MachineName,
                AppVersion: typeof(ExceptionMiddleware).Assembly.GetName().Version?.ToString(),
                Env: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"));

            ctx.Response.ContentType = "application/problem+json";
            ctx.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new {
                type = "https://tools.ietf.org/html/rfc7807", title = "Internal Server Error",
                status = 500, traceId = ctx.TraceIdentifier, timestamp = DateTime.UtcNow }));
        }
    }
    static long?   GetUid(HttpContext c) => long.TryParse(c.User.FindFirstValue("uid"), out var id) ? id : null;
    static string  GetIP (HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
}

// ============================================================
// 2. SECURITY HEADERS MIDDLEWARE
// ============================================================
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"]     = "nosniff";
        h["X-Frame-Options"]            = "DENY";
        h["X-XSS-Protection"]           = "1; mode=block";
        h["Referrer-Policy"]            = "strict-origin-when-cross-origin";
        h["Permissions-Policy"]         = "geolocation=(), microphone=(), camera=()";
        h["Strict-Transport-Security"]  = "max-age=31536000; includeSubDomains";
        h["Content-Security-Policy"]    = "default-src 'self'; frame-ancestors 'none'";
        h["Cache-Control"]              = "no-store";
        h.Remove("Server"); h.Remove("X-Powered-By");
        await next(ctx);
    }
}

// ============================================================
// 3. PERFORMANCE MIDDLEWARE â€” logs slow requests (>2s)
// ============================================================
public sealed class PerformanceMiddleware(RequestDelegate next, ILogChannel log)
{
    private const long Threshold = 2_000;
    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();
        if (sw.ElapsedMilliseconds >= Threshold)
        {
            var path = ctx.Request.Path.Value ?? "";
            log.WriteEvent(new EventLogWrite(
                $"PERF.SLOW_REQUEST", "Performance", "System",
                $"{ctx.Request.Method} {path}", "READ",
                $"Slow: {ctx.Request.Method} {path} â€” {sw.ElapsedMilliseconds}ms",
                ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0",
                Severity: "Warning", Outcome: "Partial",
                RequestID: ctx.TraceIdentifier, HTTPMethod: ctx.Request.Method,
                RequestPath: path, DurationMs: sw.ElapsedMilliseconds));
        }
    }
}

// ============================================================
// 4. EVENT LOG MIDDLEWARE â€” every request
// ============================================================
public sealed class EventLogMiddleware(RequestDelegate next, ILogChannel log)
{
    private static readonly HashSet<string> _skip = new(StringComparer.OrdinalIgnoreCase)
        { "/health", "/swagger", "/favicon.ico", "/jobs" };

    public async Task InvokeAsync(HttpContext ctx, IGeoIPService geo, IDeviceService device)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (_skip.Any(s => path.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
        { await next(ctx); return; }

        var sw = Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();

        // Capture context synchronously before recycling
        var ip      = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var ua      = ctx.Request.Headers.UserAgent.ToString();
        var traceId = ctx.TraceIdentifier;
        var method  = ctx.Request.Method;
        var status  = ctx.Response.StatusCode;
        var uid     = long.TryParse(ctx.User.FindFirstValue("uid"), out var u) ? u : (long?)null;
        var uname   = ctx.User.FindFirstValue("name");
        var role    = ctx.User.FindFirstValue("role");
        var elapsed = sw.ElapsedMilliseconds;
        var module  = ModuleOf(path);
        var outcome = status < 400 ? "Success" : status < 500 ? "ClientError" : "Failure";
        var severity= status >= 500 ? "Critical" : status >= 400 ? "Warning" : "Info";

        // Fire and forget â€” GeoIP is async, channel write is O(1)
        _ = Task.Run(async () =>
        {
            try
            {
                var geo_  = await geo.LookupAsync(ip);
                var dev_  = device.Parse(ua);
                log.WriteEvent(new EventLogWrite(
                    $"{module.ToUpper()}.{ActionOf(method)}", CategoryOf(path), module,
                    $"{method} {path}", ActionOf(method),
                    $"{method} {path} â†’ {status}", ip,
                    Severity: severity, Outcome: outcome,
                    FailureReason: outcome != "Success" ? $"HTTP {status}" : null,
                    UserID: uid, Username: uname, UserRole: role, UserAgent: ua,
                    DeviceType: dev_.DeviceType, DeviceName: dev_.DeviceName,
                    OSName: dev_.OSName, BrowserName: dev_.BrowserName,
                    Country: geo_.Country, Region: geo_.Region, City: geo_.City,
                    Lat: geo_.Lat, Lng: geo_.Lng, ISP: geo_.ISP, ASN: geo_.ASN,
                    RequestID: traceId, HTTPMethod: method, RequestPath: path,
                    DurationMs: elapsed));
            }
            catch { /* logging must never crash */ }
        });
    }

    private static string ModuleOf(string p) =>
        p.Split('/', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(2)?.ToLower() switch {
            "auth" => "Auth", "users" => "UserManagement", "roles" => "RoleManagement",
            "master" => "MasterData", "institutions" => "Institutions",
            "programs" => "ScholarshipPrograms", "applications" => "Applications",
            "students" => "StudentProfiles", "educations" => "Educations",
            "scholarships" => "Scholarships", "payments" => "Finance",
            "progress-reports" => "AcademicProgress", "extensions" => "CourseExtension",
            "terminations" => "Termination", "bsa" => "BSA",
            "reports" => "Reports", "logs" => "Logs", "notifications" => "Notifications",
            _ => "System" };

    private static string CategoryOf(string p) =>
        p.Contains("/auth") ? "Auth" : p.Contains("/payment") ? "Finance" :
        p.Contains("/application") || p.Contains("/program") ? "Scholarship" :
        p.Contains("/progress") ? "Academic" :
        p.Contains("/extension") || p.Contains("/termination") ? "Exception" :
        p.Contains("/log") || p.Contains("/report") ? "Admin" : "System";

    private static string ActionOf(string m) => m.ToUpper() switch {
        "GET" => "READ", "POST" => "CREATE", "PUT" or "PATCH" => "UPDATE", "DELETE" => "DELETE", _ => m };
}

// ============================================================
// 5. LOGIN EVENT MIDDLEWARE â€” auth paths only
// ============================================================
public sealed class LoginEventMiddleware(RequestDelegate next, ILogChannel log)
{
    private static readonly HashSet<string> _authPaths = new(StringComparer.OrdinalIgnoreCase)
        { "/api/v1/auth/login", "/api/v1/auth/ndi-login",
          "/api/v1/auth/2fa/verify", "/api/v1/auth/logout", "/api/v1/auth/refresh" };

    public async Task InvokeAsync(HttpContext ctx, IGeoIPService geo, IDeviceService device)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!_authPaths.Any(ap => path.Equals(ap, StringComparison.OrdinalIgnoreCase)))
        { await next(ctx); return; }

        // Buffer response to read status after handler
        var orig = ctx.Response.Body;
        await using var buf = new System.IO.MemoryStream();
        ctx.Response.Body = buf;
        await next(ctx);
        buf.Seek(0, System.IO.SeekOrigin.Begin);
        await buf.CopyToAsync(orig); ctx.Response.Body = orig;

        var status  = ctx.Response.StatusCode;
        var success = status is >= 200 and < 300;
        var ip      = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var ua      = ctx.Request.Headers.UserAgent.ToString();
        var uid     = long.TryParse(ctx.User.FindFirstValue("uid"), out var u) ? u : (long?)null;

        var evtType = path.Contains("logout", StringComparison.OrdinalIgnoreCase) ? "LOGOUT" :
                      path.Contains("2fa",    StringComparison.OrdinalIgnoreCase) ? (success ? "2FA_SUCCESS" : "2FA_FAIL") :
                      path.Contains("refresh")                                     ? "TOKEN_REFRESH" :
                      success                                                       ? "LOGIN_SUCCESS" :
                      status == 423                                                 ? "LOCKOUT" : "LOGIN_FAIL";

        var authMethod = path.Contains("ndi") ? "NDI" :
                         path.Contains("2fa") ? "TwoFA_TOTP" : "Password";

        _ = Task.Run(async () =>
        {
            try
            {
                var geo_ = await geo.LookupAsync(ip);
                var dev_ = device.Parse(ua);
                log.WriteLogin(new LoginLogWrite(
                    UserID: uid, CIDAttempted: null, EventType: evtType, AuthMethod: authMethod,
                    IPAddress: ip, FailureReason: success ? null : $"HTTP {status}",
                    UserAgent: ua, DeviceType: dev_.DeviceType, DeviceName: dev_.DeviceName,
                    OSName: dev_.OSName, BrowserName: dev_.BrowserName,
                    Country: geo_.Country, Region: geo_.Region, City: geo_.City,
                    Lat: geo_.Lat, Lng: geo_.Lng, ISP: geo_.ISP, ASN: geo_.ASN,
                    ThreatFlag: geo_.IsThreat, ThreatDetail: geo_.ThreatDetail));
            }
            catch { }
        });
    }
}

// ============================================================
// CURRENT USER SERVICE
// ============================================================
public sealed class CurrentUserService(IHttpContextAccessor acc) : ICurrentUser
{
    public long?   UserID    => long.TryParse(acc.HttpContext?.User.FindFirstValue("uid"), out var id) ? id : null;
    public string  Username  => acc.HttpContext?.User.FindFirstValue("name") ?? "system";
    public string  Role      => acc.HttpContext?.User.FindFirstValue("role") ?? "";
    public string  IPAddress => acc.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    public string? RequestID => acc.HttpContext?.TraceIdentifier;
    public string? UserAgent => acc.HttpContext?.Request.Headers.UserAgent.ToString();
    public bool HasPermission(string p)
        => acc.HttpContext?.User.Claims.Any(c => c.Type == "permissions" && c.Value == p) ?? false;
}


