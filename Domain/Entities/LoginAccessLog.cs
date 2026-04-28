namespace SPRMS.API.Domain.Entities;

public class LoginAccessLog
{
    public long     LoginLogID    { get; set; }
    public long?    UserID        { get; set; }
    public string?  CIDAttempted  { get; set; }
    public string   EventType     { get; set; } = "";
    public string   AuthMethod    { get; set; } = "";
    public string?  FailureReason { get; set; }
    public long?    SessionID     { get; set; }
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
    public bool     ThreatFlag    { get; set; }
    public string?  ThreatDetail  { get; set; }
    public DateTime EventOn       { get; set; } = DateTime.UtcNow;
    public string   CreatedBy     { get; set; } = "system";
    public DateTime CreatedOn     { get; set; } = DateTime.UtcNow;
}

