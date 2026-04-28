namespace SPRMS.API.Domain.Entities;

public class RolePermission : AuditEntity
{
    public long RolePermissionID { get; set; }
    public long RoleID           { get; set; }
    public long PermissionID     { get; set; }
    public Role       Role       { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

