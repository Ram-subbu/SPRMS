using System.Text;
using SPRMS.API.Application.Interfaces;
using SPRMS.API.Application.Modules.Auth;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SPRMS.API.Application.Modules.Application;
using SPRMS.Application.Modules.Financial;
using SPRMS.Application.Modules.Scholarship;
using SPRMS.Common;
using SPRMS.API.Common.Middleware;
using SPRMS.Services;
using SPRMS.Services.Background;
using SPRMS.Services.Domain;
using SPRMS.Services.Logging;
using SPRMS;
using SPRMS.API.Infrastructure.Persistence;
using LegacyAppDbContext = SPRMS.API.Infrastructure.Persistence.AppDbContext;
using WorkflowAppDbContext = SPRMS.API.Infrastructure.Persistence.AppDbContext;

// =============================================================
// SPRMS â€” Program.cs
// Single-project ASP.NET Core 8 Web API
// =============================================================

// â”€â”€ Configure Serilog early (before builder) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var loggerConfig = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/sprms-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    var cfg = builder.Configuration;
    var dbConnectionString = cfg.GetConnectionString("Default")
        ?? cfg.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string is not configured.");

    // â”€â”€ Controllers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.PropertyNamingPolicy   = JsonNamingPolicy.CamelCase;
            o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // â”€â”€ DB â€” SQL Server (existing SPRMS_V1 database) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddScoped<AuditInterceptor>();
    builder.Services.AddDbContext<LegacyAppDbContext>(options =>
        options.UseSqlServer(dbConnectionString));
    builder.Services.AddDbContext<WorkflowAppDbContext>(options =>
        options.UseSqlServer(dbConnectionString));

    // â”€â”€ Redis cache â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddStackExchangeRedisCache(o =>
        o.Configuration = cfg.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false");

    // â”€â”€ JWT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    var jwtKey = cfg["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret not set");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer  = true, ValidIssuer   = cfg["JWT:Issuer"]   ?? "SPRMS_API",
                ValidateAudience= true, ValidAudience = cfg["JWT:Audience"] ?? "SPRMS_Client",
                ValidateLifetime= true, ClockSkew     = TimeSpan.Zero,
            };
            o.Events = new JwtBearerEvents
            {
                OnChallenge = ctx =>
                {
                    ctx.HandleResponse();
                    ctx.Response.StatusCode  = 401;
                    ctx.Response.ContentType = "application/json";
                    return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                        { success = false, message = "Authentication required.", errorCode = "UNAUTHORIZED" }));
                }
            };
        });

    builder.Services.AddAuthorization();

    // â”€â”€ CORS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddCors(o => o.AddPolicy("ReactApp", p =>
        p.WithOrigins(
            cfg["AllowedOrigins:React"] ?? "http://localhost:5173",
            cfg["AllowedOrigins:Prod"]  ?? "https://sprms.rcsc.gov.bt")
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    // â”€â”€ Rate Limiting â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("api",           w => { w.PermitLimit = 100; w.Window = TimeSpan.FromMinutes(1); w.QueueLimit = 10; });
        o.AddFixedWindowLimiter("auth-sensitive",w => { w.PermitLimit = 5;   w.Window = TimeSpan.FromMinutes(5); w.QueueLimit = 0; });
        o.RejectionStatusCode = 429;
    });

    // â”€â”€ Infrastructure / Common â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
    builder.Services.AddHttpClient("geoip").SetHandlerLifetime(TimeSpan.FromMinutes(5));

    // â”€â”€ Logging stack (single project) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddSingleton<ILogChannel, LogChannel>();   // thread-safe channel
    builder.Services.AddHostedService<LogProcessorService>();   // background flush
    builder.Services.AddScoped<AuditService>();                 // business audit helper
    builder.Services.AddScoped<LogQueryService>();              // read-side queries
    builder.Services.AddScoped<IGeoIPService, GeoIPService>();  // ip-api.com
    builder.Services.AddScoped<IDeviceService, DeviceService>();// UAParser

    // â”€â”€ Register all domain services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // (Interfaces â†’ concrete implementations in Services/Domain/)
    builder.Services.AddScoped<IPasswordService, PasswordService>();
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<ApplicationService>();
    builder.Services.AddScoped<IApplicationService>(sp => sp.GetRequiredService<ApplicationService>());
    builder.Services.AddScoped<ScholarshipService>();
    builder.Services.AddScoped<IScholarshipService>(sp => sp.GetRequiredService<ScholarshipService>());
    builder.Services.AddScoped<PaymentService>();
    builder.Services.AddScoped<IPaymentService>(sp => sp.GetRequiredService<PaymentService>());

    // â”€â”€ Hangfire â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddHangfire(h =>
        h.UseSqlServerStorage(dbConnectionString));
    builder.Services.AddHangfireServer(o => o.WorkerCount = 4);

    // â”€â”€ Health Checks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddHealthChecks()
        .AddSqlServer(dbConnectionString, name: "sql-server", tags: ["db"])
        .AddRedis(cfg.GetConnectionString("Redis") ?? "localhost:6379",  name: "redis",     tags: ["cache"]);

    // â”€â”€ Swagger â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "SPRMS API",
            Version     = "v2.0",
            Description = "Scholarship Profile & Resource Management System â€” RCSC, GovTech Bhutan",
            Contact     = new OpenApiContact { Name = "GovTech Bhutan", Email = "support@govtech.bt" }
        });
        o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization", Type = SecuritySchemeType.Http,
            Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
            Description = "Enter: Bearer {your_token}"
        });
        o.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
                = Array.Empty<string>()
        });
        // Group by tag = controller name
        o.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
        o.DocInclusionPredicate((_, __) => true);
    });

    // â”€â”€ Build â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    var app = builder.Build();

    // â”€â”€ Seed database in development (test users & programs) â”€â”€â”€
    if (app.Environment.IsDevelopment())
    {
        // await SeedData.SeedDatabaseAsync(app);
    }

    // ==========================================================
    // MIDDLEWARE PIPELINE â€” ORDER IS CRITICAL
    // ==========================================================
    app.UseMiddleware<ExceptionMiddleware>(); // 1. Standard API error response
    app.UseMiddleware<SecurityHeadersMiddleware>(); // 2. OWASP security headers
    app.UseMiddleware<PerformanceMiddleware>();     // 3. Slow request detection
    app.UseMiddleware<EventLogMiddleware>();        // 4. All requests â†’ EventLogs
    app.UseMiddleware<LoginEventMiddleware>();      // 5. Auth paths â†’ LoginAccessLogs

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SPRMS API v2.0");
            c.DocumentTitle = "SPRMS API";
            c.RoutePrefix   = "swagger";
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("ReactApp");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    app.MapControllers();
    app.MapHealthChecks("/health");

    // Hangfire Dashboard (SystemAdmin only)
    app.UseHangfireDashboard("/jobs", new DashboardOptions
    {
        DashboardTitle = "SPRMS Background Jobs",
        Authorization  = [new SPRMS.HangfireAuthFilter()]
    });

    // Recurring jobs
    RecurringJob.AddOrUpdate<StipendReminderJob>("stipend-reminders",  j => j.RunAsync(), Cron.Daily(8));
    RecurringJob.AddOrUpdate<RefundAlertJob>    ("refund-alerts",      j => j.RunAsync(), Cron.Daily(8));
    RecurringJob.AddOrUpdate<CourseEndNotifyJob>("course-end-notify",  j => j.RunAsync(), Cron.Daily(7));
    RecurringJob.AddOrUpdate<HealthCheckLogJob> ("health-check-log",   j => j.RunAsync(), "*/1 * * * *");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SPRMS API failed to start");
    Console.WriteLine(ex); // 👈 ADD THIS
    throw; // 👈 IMPORTANT
}
finally { Log.CloseAndFlush(); }








