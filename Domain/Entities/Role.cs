namespace SPRMS.API.Domain.Entities;

public class Role : SoftAuditEntity
{
    public long    RoleID      { get; set; }
    public string  RoleName    { get; set; } = "";
    public string? Description { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserRole>       UserRoles       { get; set; } = [];
}

