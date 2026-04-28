namespace SPRMS.API.Domain.Entities;

public class Country : AuditEntity
{
    public long   CountryID   { get; set; }
    public string CountryName { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public bool   IsActive    { get; set; } = true;
}

