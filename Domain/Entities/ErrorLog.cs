namespace SPRMS.API.Domain.Entities;

public class ErrorLog
{
    public long     ErrorLogID      { get; set; }
    public string?  ErrorCode       { get; set; }
    public string   ErrorType       { get; set; } = "Unhandled";
    public string   Severity        { get; set; } = "Error";
    public string   Module          { get; set; } = "";
    public string?  SubModule       { get; set; }
    public string   FunctionName    { get; set; } = "";
    public string   Message         { get; set; } = "";
    public string?  StackTrace      { get; set; }
    public string?  InnerException  { get; set; }
    public string?  RequestData     { get; set; }
    public string?  ResponseData    { get; set; }
    public long?    UserID          { get; set; }
    public string?  Username        { get; set; }
    public string?  IPAddress       { get; set; }
    public string?  RequestID       { get; set; }
    public string?  HTTPMethod      { get; set; }
    public string?  RequestPath     { get; set; }
    public long?    HTTPStatusCode  { get; set; }
    public string?  EntityType      { get; set; }
    public long?    EntityID        { get; set; }
    public bool     IsResolved      { get; set; }
    public string?  ResolvedBy      { get; set; }
    public DateTime? ResolvedOn     { get; set; }
    public string?  ResolutionNotes { get; set; }
    public long     OccurrenceCount { get; set; } = 1;
    public DateTime FirstOccurredOn { get; set; } = DateTime.UtcNow;
    public DateTime LastOccurredOn  { get; set; } = DateTime.UtcNow;
    public string?  MachineName     { get; set; }
    public string?  AppVersion      { get; set; }
    public string   Environment     { get; set; } = "Production";
    public string   CreatedBy       { get; set; } = "system";
    public DateTime CreatedOn       { get; set; } = DateTime.UtcNow;
}

