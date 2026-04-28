namespace SPRMS.API.Domain.Entities;

public class AuditLog
{
    public long     AuditLogID      { get; set; }
    public string   AuditCode       { get; set; } = "";
    public string   Module          { get; set; } = "";
    public string?  SubModule       { get; set; }
    public string   FunctionName    { get; set; } = "";
    public string   Action          { get; set; } = "";
    public string   EntityType      { get; set; } = "";
    public long     EntityID        { get; set; }
    public string?  EntityReference { get; set; }
    public string   Description     { get; set; } = "";
    public string?  OldValues       { get; set; }
    public string?  NewValues       { get; set; }
    public string?  ChangedFields   { get; set; }
    public long?    AffectedUserID  { get; set; }
    public long     ActorUserID     { get; set; }
    public string   ActorUsername   { get; set; } = "";
    public string   ActorRole       { get; set; } = "";
    public string   IPAddress       { get; set; } = "";
    public string?  UserAgent       { get; set; }
    public string?  DeviceType      { get; set; }
    public string?  DeviceName      { get; set; }
    public string?  OSName          { get; set; }
    public string?  BrowserName     { get; set; }
    public string?  Country         { get; set; }
    public string?  City            { get; set; }
    public string?  ISPName         { get; set; }
    public string?  RequestID       { get; set; }
    public string?  SessionToken    { get; set; }
    public string?  ChecksumHash    { get; set; }
    public DateTime AuditOn         { get; set; } = DateTime.UtcNow;
    public string   CreatedBy       { get; set; } = "system";
    public DateTime CreatedOn       { get; set; } = DateTime.UtcNow;
}

