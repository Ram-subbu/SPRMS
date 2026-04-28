namespace SPRMS.API.Domain.Entities;

public abstract class SoftAuditEntity : AuditEntity
{
    public bool      IsActive      { get; set; } = true;
    public string?   DeactivatedBy { get; set; }
    public DateTime? DeactivatedOn { get; set; }
}

