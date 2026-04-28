namespace SPRMS.API.Domain.Entities;

public class EventLog
{
    public long     EventLogID    { get; set; }
    public string   EventCode     { get; set; } = "";
    public string   EventCategory { get; set; } = "";
    public string   Module        { get; set; } = "";
    public string?  SubModule     { get; set; }
    public string   FunctionName  { get; set; } = "";
    public string   Action        { get; set; } = "";
    public string?  EntityType    { get; set; }
    public long?    EntityID      { get; set; }
    public string   Description   { get; set; } = "";
    public string?  OldValues     { get; set; }
    public string?  NewValues     { get; set; }
    public string?  ChangedFields { get; set; }
    public string   Severity      { get; set; } = "Info";
    public string   Outcome       { get; set; } = "Success";
    public string?  FailureReason { get; set; }
    public long?    UserID        { get; set; }
    public string?  Username      { get; set; }
    public string?  UserRole      { get; set; }
    public string   IPAddress     { get; set; } = "";
    public string?  UserAgent     { get; set; }
    public string?  DeviceType    { get; set; }
    public string?  DeviceName    { get; set; }
    public string?  OSName        { get; set; }
    public string?  BrowserName   { get; set; }
    public string?  Country       { get; set; }
    public string?  Region        { get; set; }
    public string?  City          { get; set; }
    public decimal? Latitude      { get; set; }
    public decimal? Longitude     { get; set; }
    public string?  ISPName       { get; set; }
    public string?  ASNumber      { get; set; }
    public string?  RequestID     { get; set; }
    public string?  HTTPMethod    { get; set; }
    public string?  RequestPath   { get; set; }
    public long?    DurationMs    { get; set; }
    public string?  SessionToken  { get; set; }
    public DateTime EventOn       { get; set; } = DateTime.UtcNow;
    public string   CreatedBy     { get; set; } = "system";
    public DateTime CreatedOn     { get; set; } = DateTime.UtcNow;
}

