namespace SPRMS.API.Domain.Entities;

public abstract class AuditEntity
{
    public string    CreatedBy  { get; set; } = "system";
    public DateTime  CreatedOn  { get; set; } = DateTime.UtcNow;
    public string?   UpdatedBy  { get; set; }
    public DateTime? UpdatedOn  { get; set; }
    public string?   VerifiedBy { get; set; }
    public DateTime? VerifiedOn { get; set; }
}

