namespace SPRMS.API.Domain.Entities;

public class PasswordResetToken : AuditEntity
{
    public long      TokenID   { get; set; }
    public long      UserID    { get; set; }
    public string    TokenHash { get; set; } = "";
    public DateTime  ExpiresOn { get; set; }
    public DateTime? UsedOn    { get; set; }
    public string    RequestIP { get; set; } = "";
    public User      User      { get; set; } = null!;
}

