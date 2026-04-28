namespace SPRMS.API.Domain.Entities;

public class Permission : AuditEntity
{
    public long    PermissionID { get; set; }
    public string  Code         { get; set; } = "";
    public string  Module       { get; set; } = "";
    public string? Description  { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

