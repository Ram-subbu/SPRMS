using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SPRMS.Common;
using UAParser;

namespace SPRMS.Services.Logging;

// ============================================================
// GEOIP SERVICE — ip-api.com with in-memory cache
// ============================================================
public sealed class GeoIPService(IHttpClientFactory http, ILogger<GeoIPService> log) : IGeoIPService
{
    private readonly Dictionary<string, GeoInfo> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<GeoInfo> LookupAsync(string ip, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ip) || ip is "::1" or "127.0.0.1" or "0.0.0.0")
            return new GeoInfo("Localhost", null, null, null, null, null, null);
        if (_cache.TryGetValue(ip, out var hit)) return hit;
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(ip, out hit)) return hit;
            var result = await FetchAsync(ip, ct);
            _cache[ip] = result;
            return result;
        }
        catch (Exception ex) { log.LogWarning(ex, "[GeoIP] {ip}", ip); return new GeoInfo(null,null,null,null,null,null,null); }
        finally { _lock.Release(); }
    }

    private async Task<GeoInfo> FetchAsync(string ip, CancellationToken ct)
    {
        var c = http.CreateClient("geoip");
        c.Timeout = TimeSpan.FromSeconds(3);
        using var r = await c.GetAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city,lat,lon,isp,as,proxy,hosting", ct);
        if (!r.IsSuccessStatusCode) return new GeoInfo(null,null,null,null,null,null,null);
        await using var s = await r.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var root = doc.RootElement;
        if (root.TryGetProperty("status", out var st) && st.GetString() != "success")
            return new GeoInfo(null,null,null,null,null,null,null);

        string? G(string k) => root.TryGetProperty(k, out var v) ? v.GetString() : null;
        decimal? D(string k) => root.TryGetProperty(k, out var v) ? (decimal?)v.GetDecimal() : null;
        bool     B(string k) => root.TryGetProperty(k, out var v) && v.GetBoolean();

        var proxy    = B("proxy"); var hosting = B("hosting");
        var threat   = proxy || hosting;
        var detail   = threat ? (proxy ? "VPN/Proxy detected" : "Datacenter/Hosting IP") : null;
        return new GeoInfo(G("country"), G("regionName"), G("city"), D("lat"), D("lon"), G("isp"), G("as"), threat, detail);
    }
}

// ============================================================
// DEVICE SERVICE — UAParser.NET
// ============================================================
public sealed class DeviceService : IDeviceService
{
    private static readonly Parser _p = Parser.GetDefault();
    public DeviceInfo Parse(string? ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return new DeviceInfo("Unknown", null, null, null);
        try
        {
            var c = _p.Parse(ua);
            var dt = c.Device.Family == "Other"
                ? (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
                   (ua.Contains("Android") && !ua.Contains("Mobile")) ? "Tablet" : "Desktop")
                : "Mobile";
            return new DeviceInfo(dt,
                c.Device.Family == "Other" ? null : c.Device.Family,
                $"{c.OS.Family} {c.OS.Major}".Trim(),
                $"{c.UA.Family} {c.UA.Major}".Trim());
        }
        catch { return new DeviceInfo("Unknown", null, null, null); }
    }
}

// ============================================================
// AUDIT SERVICE — explicit business events
// ============================================================
public sealed class AuditService(ILogChannel log, ICurrentUser user)
{
    private static readonly JsonSerializerOptions _j = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task RecordAsync(
        string auditCode, string module, string functionName, string action,
        string entityType, long entityId, string description,
        object? oldState = null, object? newState = null,
        long? affectedUserId = null, string? entityRef = null)
    {
        var old = oldState != null ? JsonSerializer.Serialize(oldState, _j) : null;
        var nw  = newState != null ? JsonSerializer.Serialize(newState, _j) : null;
        var chf = ComputeChanged(old, nw);
        var cs  = ComputeChecksum(entityType, entityId, old, nw);

        log.WriteAudit(new AuditLogWrite(
            auditCode, module, functionName, action, entityType, entityId, description,
            user.Username, user.Role, user.IPAddress, user.RequestID,
            old, nw, chf, cs, affectedUserId, entityRef));
        return Task.CompletedTask;
    }

    public Task ApprovedAsync(string module, string entity, long id, object? before, object? after, long? affectedId = null)
        => RecordAsync($"{module}.{entity}.APPROVE", module, $"{module}.{entity}.Approve",
            "APPROVE", entity, id, $"{entity} #{id} approved by {user.Username}", before, after, affectedId);

    public Task RejectedAsync(string module, string entity, long id, string reason, object? before, long? affectedId = null)
        => RecordAsync($"{module}.{entity}.REJECT", module, $"{module}.{entity}.Reject",
            "REJECT", entity, id, $"{entity} #{id} rejected by {user.Username}. Reason: {reason}",
            before, new { status = "Rejected", reason }, affectedId);

    public Task CreatedAsync(string module, string entity, long id, object state)
        => RecordAsync($"{module}.{entity}.CREATE", module, $"{module}.{entity}.Create",
            "INSERT", entity, id, $"{entity} #{id} created by {user.Username}", null, state);

    public Task UpdatedAsync(string module, string entity, long id, object? before, object after)
        => RecordAsync($"{module}.{entity}.UPDATE", module, $"{module}.{entity}.Update",
            "UPDATE", entity, id, $"{entity} #{id} updated by {user.Username}", before, after);

    public Task DeletedAsync(string module, string entity, long id, object? before = null)
        => RecordAsync($"{module}.{entity}.DELETE", module, $"{module}.{entity}.Delete",
            "DELETE", entity, id, $"{entity} #{id} deactivated by {user.Username}",
            before, new { isActive = false });

    private static string ComputeChecksum(string et, long eid, string? o, string? n)
    {
        var p = $"{et}|{eid}|{o ?? ""}|{n ?? ""}|{DateTime.UtcNow:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p)));
    }

    private static string? ComputeChanged(string? old, string? nw)
    {
        if (old == null || nw == null) return null;
        try
        {
            var od = JsonDocument.Parse(old); var nd = JsonDocument.Parse(nw);
            var ch = nd.RootElement.EnumerateObject()
                .Where(p => !od.RootElement.TryGetProperty(p.Name, out var v) || v.ToString() != p.Value.ToString())
                .Select(p => p.Name).ToList();
            return ch.Count > 0 ? JsonSerializer.Serialize(ch) : null;
        }
        catch { return null; }
    }
}

// ============================================================
// LOG QUERY SERVICE — reads all log tables (ADO.NET)
// ============================================================
public sealed class LogQueryService(IConfiguration cfg)
{
    private readonly string _conn = cfg.GetConnectionString("DefaultConnection")!;

    // ── EventLogs ─────────────────────────────────────────────
    public async Task<PagedResult<EventLogDto>> GetEventsAsync(EventLogFilter f, CancellationToken ct)
    {
        var (w, ps) = BuildWhere(new[] {
            ("Module",   f.Module,   "="),  ("Action",   f.Action,   "="),
            ("Severity", f.Severity, "="),  ("Outcome",  f.Outcome,  "="),
            ("IPAddress",f.IPAddress,"="),  ("Username", f.Search,   "LIKE")
        });
        if (f.UserID.HasValue) { w.Add("UserID=@uid"); ps.Add(P("@uid",f.UserID)); }
        if (!string.IsNullOrEmpty(f.DateFrom)) { w.Add("EventOn>=@df"); ps.Add(P("@df",f.DateFrom)); }
        if (!string.IsNullOrEmpty(f.DateTo))   { w.Add("EventOn<=@dt"); ps.Add(P("@dt",f.DateTo)); }

        var sql = $@"SELECT COUNT(*)OVER() AS Total,EventLogID,EventCode,Module,SubModule,FunctionName,Action,
                    EntityType,EntityID,Description,OldValues,NewValues,Severity,Outcome,FailureReason,
                    Username,UserRole,IPAddress,DeviceType,OSName,BrowserName,Country,City,ISPName,RequestPath,DurationMs,EventOn
                    FROM dbo.EventLogs {Clause(w)} ORDER BY EventOn DESC
                    OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY";
        return await QueryAsync<EventLogDto>(sql, ps, (f.Page-1)*f.PageSize, f.PageSize,
            r => new EventLogDto(r.GetInt64(1),r.S(2),r.S(3),r.SN(4),r.S(5),r.S(6),r.SN(7),r.LN(8),
                r.S(9),r.SN(10),r.SN(11),r.S(12),r.S(13),r.SN(14),r.SN(15),r.SN(16),
                r.S(17),r.SN(18),r.SN(19),r.SN(20),r.SN(21),r.SN(22),r.SN(23),r.LN(24),r.GetDateTime(25)), ct);
    }

    // ── LoginAccessLogs ───────────────────────────────────────
    public async Task<PagedResult<LoginLogDto>> GetLoginsAsync(LoginLogFilter f, CancellationToken ct)
    {
        var (w, ps) = BuildWhere(new[] {
            ("l.IPAddress",f.IPAddress,"="), ("l.EventType",f.EventType,"="), ("l.Country",f.Country,"=")
        });
        if (f.UserID.HasValue)   { w.Add("l.UserID=@uid"); ps.Add(P("@uid",f.UserID)); }
        if (f.ThreatOnly==true)  w.Add("l.ThreatFlag=1");
        if (!string.IsNullOrEmpty(f.DateFrom)) { w.Add("l.EventOn>=@df"); ps.Add(P("@df",f.DateFrom)); }
        if (!string.IsNullOrEmpty(f.DateTo))   { w.Add("l.EventOn<=@dt"); ps.Add(P("@dt",f.DateTo)); }

        var sql = $@"SELECT COUNT(*)OVER() AS Total,l.LoginLogID,u.FullName,l.CIDAttempted,l.EventType,l.AuthMethod,
                    l.FailureReason,l.IPAddress,l.DeviceType,l.DeviceName,l.OSName,l.BrowserName,
                    l.Country,l.City,l.ISPName,l.ThreatFlag,l.ThreatDetail,l.EventOn
                    FROM dbo.LoginAccessLogs l LEFT JOIN dbo.Users u ON u.UserID=l.UserID
                    {Clause(w,"l")} ORDER BY l.EventOn DESC
                    OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY";
        return await QueryAsync<LoginLogDto>(sql, ps, (f.Page-1)*f.PageSize, f.PageSize,
            r => new LoginLogDto(r.GetInt64(1),r.SN(2),r.SN(3),r.S(4),r.S(5),r.SN(6),
                r.S(7),r.SN(8),r.SN(9),r.SN(10),r.SN(11),r.SN(12),r.SN(13),r.SN(14),
                r.GetBoolean(15),r.SN(16),r.GetDateTime(17)), ct);
    }

    // ── AuditLogs ─────────────────────────────────────────────
    public async Task<PagedResult<AuditLogDto>> GetAuditsAsync(AuditLogFilter f, CancellationToken ct)
    {
        var (w, ps) = BuildWhere(new[] {
            ("EntityType",f.EntityType,"="),("Module",f.Module,"="),("Action",f.Action,"=")
        });
        if (f.EntityID.HasValue)    { w.Add("EntityID=@eid");    ps.Add(P("@eid",f.EntityID)); }
        if (f.ActorUserID.HasValue) { w.Add("ActorUserID=@auid");ps.Add(P("@auid",f.ActorUserID)); }
        if (!string.IsNullOrEmpty(f.DateFrom)) { w.Add("AuditOn>=@df"); ps.Add(P("@df",f.DateFrom)); }
        if (!string.IsNullOrEmpty(f.DateTo))   { w.Add("AuditOn<=@dt"); ps.Add(P("@dt",f.DateTo)); }

        var sql = $@"SELECT COUNT(*)OVER() AS Total,AuditLogID,AuditCode,Module,SubModule,Action,
                    EntityType,EntityID,EntityReference,Description,OldValues,NewValues,ChangedFields,
                    ActorUsername,ActorRole,IPAddress,Country,City,ChecksumHash,AuditOn
                    FROM dbo.AuditLogs {Clause(w)} ORDER BY AuditOn DESC
                    OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY";
        return await QueryAsync<AuditLogDto>(sql, ps, (f.Page-1)*f.PageSize, f.PageSize,
            r => new AuditLogDto(r.GetInt64(1),r.S(2),r.S(3),r.SN(4),r.S(5),r.S(6),r.GetInt64(7),
                r.SN(8),r.S(9),r.SN(10),r.SN(11),r.SN(12),r.S(13),r.S(14),r.S(15),
                r.SN(16),r.SN(17),r.SN(18),r.GetDateTime(19)), ct);
    }

    // ── Checksum verify ───────────────────────────────────────
    public async Task<ChecksumResult> VerifyChecksumAsync(long id, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_conn);
        await using var cmd  = new SqlCommand("SELECT AuditLogID,EntityType,EntityID,OldValues,NewValues,AuditOn,ChecksumHash FROM dbo.AuditLogs WHERE AuditLogID=@id", conn);
        cmd.Parameters.Add(P("@id", id));
        await conn.OpenAsync(ct);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return new ChecksumResult(id, null, null, false, "Not found");
        var et=r.S(1); var eid=r.GetInt64(2); var ov=r.SN(3)??""; var nv=r.SN(4)??"";
        var ao=r.GetDateTime(5).ToString("O"); var stored=r.SN(6);
        var computed=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{et}|{eid}|{ov}|{nv}|{ao}")));
        var intact=string.Equals(stored,computed,StringComparison.OrdinalIgnoreCase);
        return new ChecksumResult(id,stored,computed,intact,intact?"INTACT":"TAMPERED");
    }

    // ── ErrorLogs ─────────────────────────────────────────────
    public async Task<PagedResult<ErrorLogDto>> GetErrorsAsync(ErrorLogFilter f, CancellationToken ct)
    {
        var (w, ps) = BuildWhere(new[] {
            ("Module",f.Module,"="),("Severity",f.Severity,"="),("ErrorType",f.ErrorType,"="),("Environment",f.Environment,"=")
        });
        if (f.IsResolved.HasValue) { w.Add("IsResolved=@ir"); ps.Add(P("@ir",f.IsResolved.Value?1:0)); }
        if (!string.IsNullOrEmpty(f.DateFrom)) { w.Add("LastOccurredOn>=@df"); ps.Add(P("@df",f.DateFrom)); }
        if (!string.IsNullOrEmpty(f.DateTo))   { w.Add("LastOccurredOn<=@dt"); ps.Add(P("@dt",f.DateTo)); }

        var sql = $@"SELECT COUNT(*)OVER() AS Total,ErrorLogID,ErrorCode,ErrorType,Severity,Module,FunctionName,
                    Message,StackTrace,HTTPStatusCode,IsResolved,ResolvedBy,OccurrenceCount,FirstOccurredOn,LastOccurredOn,Environment
                    FROM dbo.ErrorLogs {Clause(w)} ORDER BY LastOccurredOn DESC
                    OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY";
        return await QueryAsync<ErrorLogDto>(sql, ps, (f.Page-1)*f.PageSize, f.PageSize,
            r => new ErrorLogDto(r.GetInt64(1),r.SN(2),r.S(3),r.S(4),r.S(5),r.S(6),r.S(7),
                r.SN(8),r.LN(9),r.GetBoolean(10),r.SN(11),r.GetInt64(12),
                r.GetDateTime(13),r.GetDateTime(14),r.S(15)), ct);
    }

    public async Task<bool> ResolveErrorAsync(long id, string notes, string resolvedBy, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_conn);
        await using var cmd = new SqlCommand("UPDATE dbo.ErrorLogs SET IsResolved=1,ResolvedBy=@rb,ResolvedOn=GETDATE(),ResolutionNotes=@n WHERE ErrorLogID=@id AND IsResolved=0", conn);
        cmd.Parameters.AddRange([P("@id",id),P("@rb",resolvedBy),P("@n",notes)]);
        await conn.OpenAsync(ct);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ── Helpers ───────────────────────────────────────────────
    private static (List<string> w, List<SqlParameter> ps) BuildWhere(IEnumerable<(string col,string? val,string op)> defs)
    {
        var w = new List<string>(); var ps = new List<SqlParameter>();
        foreach (var (col, val, op) in defs)
        {
            if (string.IsNullOrEmpty(val)) continue;
            var n = "@" + col.Replace(".","_");
            w.Add(op == "LIKE" ? $"({col} LIKE {n} OR Module LIKE {n})" : $"{col}{op}{n}");
            ps.Add(P(n, op == "LIKE" ? $"%{val}%" : val));
        }
        return (w, ps);
    }

    private static string Clause(List<string> w, string? alias = null)
        => w.Count > 0 ? "WHERE " + string.Join(" AND ", w) : "";

    private async Task<PagedResult<T>> QueryAsync<T>(string sql, List<SqlParameter> ps, int offset, int pageSize, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_conn);
        await using var cmd  = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(ps.ToArray());
        cmd.Parameters.AddRange([P("@off",offset), P("@ps",pageSize)]);
        await conn.OpenAsync(ct);
        var items = new List<T>(); long total = 0;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) { total = r.GetInt64(0); items.Add(map(r)); }
        return new PagedResult<T> { Items = items, Page = offset/pageSize+1, PageSize = pageSize, TotalCount = total };
    }

    private static SqlParameter P(string n, object? v) => new(n, v ?? (object)DBNull.Value);
}

// ── Reader helpers extension ──────────────────────────────────
internal static class ReaderExt
{
    public static string  S (this SqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
    public static string? SN(this SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    public static long?   LN(this SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);
}

// ── Log Query DTOs ────────────────────────────────────────────
public record EventLogFilter(string? Module=null,string? Action=null,string? Severity=null,string? Outcome=null,string? IPAddress=null,long? UserID=null,string? DateFrom=null,string? DateTo=null,string? Search=null,int Page=1,int PageSize=50);
public record LoginLogFilter(long? UserID=null,string? IPAddress=null,string? EventType=null,string? Country=null,bool? ThreatOnly=null,string? DateFrom=null,string? DateTo=null,int Page=1,int PageSize=50);
public record AuditLogFilter(string? EntityType=null,long? EntityID=null,string? Module=null,string? Action=null,long? ActorUserID=null,string? DateFrom=null,string? DateTo=null,int Page=1,int PageSize=50);
public record ErrorLogFilter(string? Module=null,string? Severity=null,string? ErrorType=null,bool? IsResolved=null,string? Environment=null,string? DateFrom=null,string? DateTo=null,int Page=1,int PageSize=50);
public record EventLogDto(long EventLogID,string EventCode,string Module,string? SubModule,string FunctionName,string Action,string? EntityType,long? EntityID,string Description,string? OldValues,string? NewValues,string Severity,string Outcome,string? FailureReason,string? Username,string? UserRole,string IPAddress,string? DeviceType,string? OSName,string? BrowserName,string? Country,string? City,string? ISPName,long? DurationMs,DateTime EventOn);
public record LoginLogDto(long LoginLogID,string? Username,string? CIDAttempted,string EventType,string AuthMethod,string? FailureReason,string IPAddress,string? DeviceType,string? DeviceName,string? OSName,string? BrowserName,string? Country,string? City,string? ISPName,bool ThreatFlag,string? ThreatDetail,DateTime EventOn);
public record AuditLogDto(long AuditLogID,string AuditCode,string Module,string? SubModule,string Action,string EntityType,long EntityID,string? EntityReference,string Description,string? OldValues,string? NewValues,string? ChangedFields,string ActorUsername,string ActorRole,string IPAddress,string? Country,string? City,string? ChecksumHash,DateTime AuditOn);
public record ErrorLogDto(long ErrorLogID,string? ErrorCode,string ErrorType,string Severity,string Module,string FunctionName,string Message,string? StackTrace,long? HTTPStatusCode,bool IsResolved,string? ResolvedBy,long OccurrenceCount,DateTime FirstOccurredOn,DateTime LastOccurredOn,string Environment);
public record ChecksumResult(long AuditLogID,string? StoredHash,string? ComputedHash,bool Intact,string Status);
