using System.Threading.Channels;

namespace SPRMS.Common;

public class LogItem { }

public sealed class EventItem(EventLogWrite e) : LogItem
{
    public EventLogWrite Entry => e;
}

public sealed class LoginItem(LoginLogWrite e) : LogItem
{
    public LoginLogWrite Entry => e;
}

public sealed class AuditItem(AuditLogWrite e) : LogItem
{
    public AuditLogWrite Entry => e;
}

public sealed class ErrorItem(ErrorLogWrite e) : LogItem
{
    public ErrorLogWrite Entry => e;
}

public sealed class IntItem(IntLogWrite e) : LogItem
{
    public IntLogWrite Entry => e;
}

public interface ILogChannel
{
    void WriteEvent(EventLogWrite e);
    void WriteLogin(LoginLogWrite e);
    void WriteAudit(AuditLogWrite e);
    void WriteError(ErrorLogWrite e);
    void WriteInt(IntLogWrite e);
    ChannelReader<LogItem> Reader { get; }
}

public sealed class LogChannel : ILogChannel
{
    private readonly Channel<LogItem> _ch = Channel.CreateBounded<LogItem>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });

    public void WriteEvent(EventLogWrite e) => _ch.Writer.TryWrite(new EventItem(e));
    public void WriteLogin(LoginLogWrite e) => _ch.Writer.TryWrite(new LoginItem(e));
    public void WriteAudit(AuditLogWrite e) => _ch.Writer.TryWrite(new AuditItem(e));
    public void WriteError(ErrorLogWrite e) => _ch.Writer.TryWrite(new ErrorItem(e));
    public void WriteInt(IntLogWrite e) => _ch.Writer.TryWrite(new IntItem(e));
    public ChannelReader<LogItem> Reader => _ch.Reader;
}

public record EventLogWrite(
    string EventCode = "GENERAL", string EventCategory = "System", string Module = "System", string FunctionName = "",
    string Action = "", string Description = "", string IPAddress = "",
    string? SubModule = null, string? EntityType = null, long? EntityID = null,
    string? OldValues = null, string? NewValues = null, string? ChangedFields = null,
    string Severity = "Info", string Outcome = "Success", string? FailureReason = null,
    long? UserID = null, string? Username = null, string? UserRole = null,
    string? UserAgent = null, string? DeviceType = null, string? DeviceName = null,
    string? OSName = null, string? BrowserName = null,
    string? Country = null, string? Region = null, string? City = null,
    decimal? Lat = null, decimal? Lng = null, string? ISP = null, string? ASN = null,
    string? RequestID = null, string? HTTPMethod = null, string? RequestPath = null,
    long? DurationMs = null);

public record LoginLogWrite(
    long? UserID, string? CIDAttempted, string EventType, string AuthMethod,
    string IPAddress, string? FailureReason = null, long? SessionID = null,
    string? UserAgent = null, string? DeviceType = null, string? DeviceName = null,
    string? OSName = null, string? BrowserName = null,
    string? Country = null, string? Region = null, string? City = null,
    decimal? Lat = null, decimal? Lng = null, string? ISP = null, string? ASN = null,
    bool ThreatFlag = false, string? ThreatDetail = null);

public record AuditLogWrite(
    string AuditCode, string Module, string FunctionName, string Action,
    string EntityType, long EntityID, string Description,
    string ActorUsername, string ActorRole, string IPAddress, string? RequestID,
    string? OldValues = null, string? NewValues = null, string? ChangedFields = null,
    string? ChecksumHash = null, long? AffectedUserID = null, string? EntityReference = null,
    string? Country = null, string? City = null, string? ISP = null);

public record ErrorLogWrite(
    string Module, string FunctionName, string Message,
    string ErrorType = "Unhandled", string Severity = "Error",
    string? ErrorCode = null, string? SubModule = null,
    string? StackTrace = null, string? InnerException = null, string? RequestData = null,
    long? UserID = null, string? IPAddress = null, string? RequestID = null,
    string? HTTPMethod = null, string? RequestPath = null, long? HttpStatus = null,
    string? EntityType = null, long? EntityID = null,
    string? MachineName = null, string? AppVersion = null, string Env = "Production");

public record IntLogWrite(
    string ExternalSystem, string OperationName, string ErrorMessage,
    string? RequestPayload = null, string? ResponsePayload = null,
    long? HttpStatus = null, bool IsRetryable = false,
    string? EntityType = null, long? EntityID = null);

