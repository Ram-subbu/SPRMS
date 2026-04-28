namespace SPRMS.API.Domain.Entities;

public class UserRole : AuditEntity
{
    public long      UserRoleID { get; set; }
    public long      UserID     { get; set; }
    public long      RoleID     { get; set; }
    public long      AssignedBy { get; set; }
    public DateTime  AssignedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresOn  { get; set; }
    public User      User       { get; set; } = null!;
    public Role      Role       { get; set; } = null!;
}

