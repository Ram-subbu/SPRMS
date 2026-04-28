namespace SPRMS.API.Domain.Entities;

public class UserSession : AuditEntity
{
    public long      SessionID             { get; set; }
    public long      UserID                { get; set; }
    public string    SessionToken          { get; set; } = "";
    public string?   RefreshToken          { get; set; }
    public DateTime? RefreshTokenExpiresOn { get; set; }
    public DateTime  IssuedOn              { get; set; } = DateTime.UtcNow;
    public DateTime  ExpiresOn             { get; set; }
    public DateTime? RevokedOn             { get; set; }
    public string?   RevokedReason         { get; set; }
    public string    IPAddress             { get; set; } = "";
    public string?   UserAgent             { get; set; }
    public string?   DeviceType            { get; set; }
    public string?   DeviceName            { get; set; }
    public string?   OSName                { get; set; }
    public string?   BrowserName           { get; set; }
    public string?   Country               { get; set; }
    public string?   Region                { get; set; }
    public string?   City                  { get; set; }
    public decimal?  Latitude              { get; set; }
    public decimal?  Longitude             { get; set; }
    public string?   ISPName               { get; set; }
    public string?   ASNumber              { get; set; }
    public bool      IsActive              { get; set; } = true;
    public User      User                  { get; set; } = null!;
}

