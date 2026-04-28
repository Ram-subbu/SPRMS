namespace SPRMS.API.Domain.Entities;

public class ExtensionReason : AuditEntity
{
    public long    ReasonID     { get; set; }
    public string  ReasonName   { get; set; } = "";
    public string? RequiredDocs { get; set; }
    public bool    IsActive     { get; set; } = true;
}

