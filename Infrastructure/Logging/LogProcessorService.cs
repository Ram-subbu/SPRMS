using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SPRMS.Common;

namespace SPRMS.Services.Logging;

// ============================================================
// LOG PROCESSOR — hosted background service
// Drains ILogChannel at up to 50 items / 500ms flush.
// Uses raw ADO.NET multi-row INSERT — zero EF overhead.
// ============================================================
public sealed class LogProcessorService(
    ILogChannel log, IConfiguration cfg,
    ILogger<LogProcessorService> logger) : BackgroundService
{
    private const int  BatchSize = 50;
    private const int  FlushMs   = 500;
    private readonly string _conn = cfg.GetConnectionString("DefaultConnection")!;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var batch = new List<LogItem>(BatchSize);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stop);
                cts.CancelAfter(FlushMs);
                try
                {
                    await foreach (var item in log.Reader.ReadAllAsync(cts.Token))
                    {
                        batch.Add(item);
                        if (batch.Count >= BatchSize) break;
                    }
                }
                catch (OperationCanceledException) { }

                if (batch.Count > 0) { await FlushAsync(batch, stop); batch.Clear(); }
            }
            catch (Exception ex) { logger.LogError(ex, "[LogProcessor] flush error"); await Task.Delay(1000, stop); }
        }
        // Drain on shutdown
        while (log.Reader.TryRead(out var item)) batch.Add(item);
        if (batch.Count > 0) await FlushAsync(batch, CancellationToken.None);
    }

    private async Task FlushAsync(List<LogItem> batch, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_conn);
        await conn.OpenAsync(ct);

        var events  = batch.OfType<EventItem>().Select(x => x.Entry).ToList();
        var logins  = batch.OfType<LoginItem>().Select(x => x.Entry).ToList();
        var audits  = batch.OfType<AuditItem>().Select(x => x.Entry).ToList();
        var errors  = batch.OfType<ErrorItem>().Select(x => x.Entry).ToList();
        var ints    = batch.OfType<IntItem>()  .Select(x => x.Entry).ToList();

        if (events.Count > 0) await InsertEventsAsync(conn, events, ct);
        if (logins.Count > 0) await InsertLoginsAsync(conn, logins, ct);
        if (audits.Count > 0) await InsertAuditsAsync(conn, audits, ct);
        if (errors.Count > 0) await UpsertErrorsAsync(conn, errors, ct);
        if (ints.Count   > 0) await InsertIntsAsync  (conn, ints,   ct);
    }

    // ── EventLogs ─────────────────────────────────────────────
    private static async Task InsertEventsAsync(SqlConnection c, List<EventLogWrite> items, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var ps = new List<SqlParameter>();
        sb.Append(@"INSERT INTO dbo.EventLogs(EventCode,EventCategory,Module,SubModule,FunctionName,Action,EntityType,EntityID,Description,OldValues,NewValues,ChangedFields,Severity,Outcome,FailureReason,UserID,Username,UserRole,IPAddress,UserAgent,DeviceType,DeviceName,OSName,BrowserName,Country,Region,City,Latitude,Longitude,ISPName,ASNumber,RequestID,HTTPMethod,RequestPath,DurationMs,EventOn,CreatedBy,CreatedOn) VALUES ");
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i]; var s = $"_{i}";
            if (i > 0) sb.Append(',');
            sb.Append($"(@ec{s},@ecat{s},@mod{s},@sub{s},@fn{s},@act{s},@et{s},@eid{s},@des{s},@old{s},@new{s},@chf{s},@sev{s},@out{s},@fr{s},@uid{s},@un{s},@ur{s},@ip{s},@ua{s},@dt{s},@dn{s},@os{s},@br{s},@co{s},@reg{s},@ci{s},@lat{s},@lng{s},@isp{s},@asn{s},@rid{s},@hm{s},@rp{s},@dur{s},GETDATE(),@cby{s},GETDATE())");
            ps.AddRange([
                P($"@ec{s}",e.EventCode),P($"@ecat{s}",e.EventCategory),P($"@mod{s}",e.Module),P($"@sub{s}",e.SubModule),
                P($"@fn{s}",e.FunctionName),P($"@act{s}",e.Action),P($"@et{s}",e.EntityType),PN($"@eid{s}",e.EntityID),
                P($"@des{s}",e.Description),P($"@old{s}",e.OldValues),P($"@new{s}",e.NewValues),P($"@chf{s}",e.ChangedFields),
                P($"@sev{s}",e.Severity),P($"@out{s}",e.Outcome),P($"@fr{s}",e.FailureReason),
                PN($"@uid{s}",e.UserID),P($"@un{s}",e.Username),P($"@ur{s}",e.UserRole),
                P($"@ip{s}",e.IPAddress),P($"@ua{s}",e.UserAgent),P($"@dt{s}",e.DeviceType),P($"@dn{s}",e.DeviceName),
                P($"@os{s}",e.OSName),P($"@br{s}",e.BrowserName),
                P($"@co{s}",e.Country),P($"@reg{s}",e.Region),P($"@ci{s}",e.City),
                PN($"@lat{s}",e.Lat),PN($"@lng{s}",e.Lng),P($"@isp{s}",e.ISP),P($"@asn{s}",e.ASN),
                P($"@rid{s}",e.RequestID),P($"@hm{s}",e.HTTPMethod),P($"@rp{s}",e.RequestPath),
                PN($"@dur{s}",e.DurationMs),P($"@cby{s}",e.Username ?? "system"),
            ]);
        }
        await using var cmd = new SqlCommand(sb.ToString(), c);
        cmd.Parameters.AddRange(ps.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── LoginAccessLogs ───────────────────────────────────────
    private static async Task InsertLoginsAsync(SqlConnection c, List<LoginLogWrite> items, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var ps = new List<SqlParameter>();
        sb.Append(@"INSERT INTO dbo.LoginAccessLogs(UserID,CIDAttempted,EventType,AuthMethod,FailureReason,SessionID,IPAddress,UserAgent,DeviceType,DeviceName,OSName,BrowserName,Country,Region,City,Latitude,Longitude,ISPName,ASNumber,ThreatFlag,ThreatDetail,EventOn,CreatedBy,CreatedOn) VALUES ");
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i]; var s = $"_{i}";
            if (i > 0) sb.Append(',');
            sb.Append($"(@uid{s},@cid{s},@et{s},@am{s},@fr{s},@sid{s},@ip{s},@ua{s},@dt{s},@dn{s},@os{s},@br{s},@co{s},@reg{s},@ci{s},@lat{s},@lng{s},@isp{s},@asn{s},@tf{s},@td{s},GETDATE(),@cby{s},GETDATE())");
            ps.AddRange([
                PN($"@uid{s}",e.UserID),P($"@cid{s}",e.CIDAttempted),P($"@et{s}",e.EventType),P($"@am{s}",e.AuthMethod),
                P($"@fr{s}",e.FailureReason),PN($"@sid{s}",e.SessionID),P($"@ip{s}",e.IPAddress),P($"@ua{s}",e.UserAgent),
                P($"@dt{s}",e.DeviceType),P($"@dn{s}",e.DeviceName),P($"@os{s}",e.OSName),P($"@br{s}",e.BrowserName),
                P($"@co{s}",e.Country),P($"@reg{s}",e.Region),P($"@ci{s}",e.City),
                PN($"@lat{s}",e.Lat),PN($"@lng{s}",e.Lng),P($"@isp{s}",e.ISP),P($"@asn{s}",e.ASN),
                BIT($"@tf{s}",e.ThreatFlag),P($"@td{s}",e.ThreatDetail),P($"@cby{s}","system"),
            ]);
        }
        await using var cmd = new SqlCommand(sb.ToString(), c);
        cmd.Parameters.AddRange(ps.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── AuditLogs ─────────────────────────────────────────────
    private static async Task InsertAuditsAsync(SqlConnection c, List<AuditLogWrite> items, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var ps = new List<SqlParameter>();
        sb.Append(@"INSERT INTO dbo.AuditLogs(AuditCode,Module,FunctionName,Action,EntityType,EntityID,EntityReference,Description,OldValues,NewValues,ChangedFields,AffectedUserID,ActorUserID,ActorUsername,ActorRole,IPAddress,Country,City,ISPName,RequestID,ChecksumHash,AuditOn,CreatedBy,CreatedOn) VALUES ");
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i]; var s = $"_{i}";
            if (i > 0) sb.Append(',');
            // Recompute checksum if not provided
            var cs = e.ChecksumHash ?? ComputeChecksum(e.EntityType, e.EntityID, e.OldValues, e.NewValues);
            sb.Append($"(@ac{s},@mod{s},@fn{s},@act{s},@et{s},@eid{s},@eref{s},@des{s},@old{s},@new{s},@chf{s},@afid{s},@auid{s},@aun{s},@ar{s},@ip{s},@co{s},@ci{s},@isp{s},@rid{s},@cs{s},GETDATE(),@cby{s},GETDATE())");
            // ActorUserID: try parse from ActorUsername, fallback 0
            ps.AddRange([
                P($"@ac{s}",e.AuditCode),P($"@mod{s}",e.Module),P($"@fn{s}",e.FunctionName),P($"@act{s}",e.Action),
                P($"@et{s}",e.EntityType),PN($"@eid{s}",e.EntityID),P($"@eref{s}",e.EntityReference),
                P($"@des{s}",e.Description),P($"@old{s}",e.OldValues),P($"@new{s}",e.NewValues),P($"@chf{s}",e.ChangedFields),
                PN($"@afid{s}",e.AffectedUserID),PN($"@auid{s}",(object?)null),// no UserID in write record — set by actor
                P($"@aun{s}",e.ActorUsername),P($"@ar{s}",e.ActorRole),P($"@ip{s}",e.IPAddress),
                P($"@co{s}",e.Country),P($"@ci{s}",e.City),P($"@isp{s}",e.ISP),
                P($"@rid{s}",e.RequestID),P($"@cs{s}",cs),P($"@cby{s}",e.ActorUsername),
            ]);
        }
        await using var cmd = new SqlCommand(sb.ToString(), c);
        cmd.Parameters.AddRange(ps.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── ErrorLogs (upsert dedup) ──────────────────────────────
    private static async Task UpsertErrorsAsync(SqlConnection c, List<ErrorLogWrite> items, CancellationToken ct)
    {
        foreach (var e in items)
        {
            const string sql = @"
                IF EXISTS(SELECT 1 FROM dbo.ErrorLogs WHERE FunctionName=@fn AND Message=@msg AND Environment=@env AND IsResolved=0)
                    UPDATE dbo.ErrorLogs SET OccurrenceCount+=1, LastOccurredOn=GETDATE()
                    WHERE  FunctionName=@fn AND Message=@msg AND Environment=@env AND IsResolved=0
                ELSE
                    INSERT INTO dbo.ErrorLogs(ErrorCode,ErrorType,Severity,Module,SubModule,FunctionName,Message,StackTrace,InnerException,RequestData,UserID,IPAddress,RequestID,HTTPMethod,RequestPath,HTTPStatusCode,EntityType,EntityID,IsResolved,OccurrenceCount,FirstOccurredOn,LastOccurredOn,MachineName,AppVersion,Environment,CreatedBy,CreatedOn)
                    VALUES(@ec,@et,@sev,@mod,@sub,@fn,@msg,@st,@ie,@rd,@uid,@ip,@rid,@hm,@rp,@hs,@etype,@eid,0,1,GETDATE(),GETDATE(),@mn,@av,@env,@cby,GETDATE())";
            await using var cmd = new SqlCommand(sql, c);
            cmd.Parameters.AddRange([
                P("@ec",e.ErrorCode),P("@et",e.ErrorType),P("@sev",e.Severity),
                P("@mod",e.Module),P("@sub",e.SubModule),P("@fn",e.FunctionName),
                P("@msg",e.Message),P("@st",e.StackTrace),P("@ie",e.InnerException),P("@rd",e.RequestData),
                PN("@uid",e.UserID),P("@ip",e.IPAddress),P("@rid",e.RequestID),
                P("@hm",e.HTTPMethod),P("@rp",e.RequestPath),PN("@hs",e.HttpStatus),
                P("@etype",e.EntityType),PN("@eid",e.EntityID),
                P("@mn",e.MachineName),P("@av",e.AppVersion),P("@env",e.Env),P("@cby","system"),
            ]);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ── IntegrationErrorLogs ──────────────────────────────────
    private static async Task InsertIntsAsync(SqlConnection c, List<IntLogWrite> items, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var ps = new List<SqlParameter>();
        sb.Append(@"INSERT INTO dbo.IntegrationErrorLogs(ExternalSystem,OperationName,RequestPayload,ResponsePayload,HTTPStatusCode,ErrorMessage,RetryCount,IsRetryable,EntityType,EntityID,CreatedBy,CreatedOn) VALUES ");
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i]; var s = $"_{i}";
            if (i > 0) sb.Append(',');
            sb.Append($"(@es{s},@op{s},@rq{s},@rs{s},@hs{s},@em{s},0,@ir{s},@et{s},@eid{s},'system',GETDATE())");
            ps.AddRange([
                P($"@es{s}",e.ExternalSystem),P($"@op{s}",e.OperationName),P($"@rq{s}",e.RequestPayload),
                P($"@rs{s}",e.ResponsePayload),PN($"@hs{s}",e.HttpStatus),P($"@em{s}",e.ErrorMessage),
                BIT($"@ir{s}",e.IsRetryable),P($"@et{s}",e.EntityType),PN($"@eid{s}",e.EntityID),
            ]);
        }
        await using var cmd = new SqlCommand(sb.ToString(), c);
        cmd.Parameters.AddRange(ps.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string ComputeChecksum(string et, long eid, string? old, string? nw)
    {
        var p = $"{et}|{eid}|{old ?? ""}|{nw ?? ""}|{DateTime.UtcNow:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p)));
    }

    private static SqlParameter P  (string n, object? v) => new(n, v ?? (object)DBNull.Value);
    private static SqlParameter PN (string n, object? v) => new(n, v ?? (object)DBNull.Value);
    private static SqlParameter BIT(string n, bool v)    => new(n, v ? 1 : 0) { SqlDbType = System.Data.SqlDbType.Bit };
}
