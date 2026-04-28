namespace SPRMS.API.Domain.Entities;

public class TwoFABackupCode : AuditEntity
{
    public long      BackupCodeID { get; set; }
    public long      UserID       { get; set; }
    public string    CodeHash     { get; set; } = "";
    public DateTime? UsedOn       { get; set; }
    public User      User         { get; set; } = null!;
}

