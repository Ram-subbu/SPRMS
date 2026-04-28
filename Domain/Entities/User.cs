using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class User : SoftAuditEntity
{
    public long      UserID                 { get; set; }
    public string    CIDNumber              { get; set; } = "";
    public string    FullName               { get; set; } = "";
    public string    Email                  { get; set; } = "";
    public string?   Phone                  { get; set; }
    public string?   NDISubjectID           { get; set; }
    public string?   PasswordHash           { get; set; }
    public string?   PasswordSalt           { get; set; }
    public DateTime? PasswordChangedOn      { get; set; }
    public int       PasswordExpiryDays     { get; set; } = 90;
    public bool      MustChangePassword     { get; set; }
    public string?   PreviousPasswordHashes { get; set; }
    public bool      TwoFAEnabled           { get; set; }
    public string?   TwoFAMethod            { get; set; }
    public string?   TwoFASecretKey         { get; set; }
    public DateTime? TwoFAVerifiedOn        { get; set; }
    public byte      FailedLoginCount       { get; set; }
    public DateTime? LockedUntil            { get; set; }
    public DateTime? LastLoginOn            { get; set; }
    public string?   LastLoginIP            { get; set; }
    public Status    Status                 { get; set; } = Status.Active;
    public ICollection<UserRole>    UserRoles   { get; set; } = [];
    public ICollection<UserSession> Sessions    { get; set; } = [];
}

