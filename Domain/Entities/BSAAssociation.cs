using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class BSAAssociation : SoftAuditEntity
{
    public long    BSAID            { get; set; }
    public long    CountryID        { get; set; }
    public string  City             { get; set; } = "";
    public string? InstitutionRef   { get; set; }
    public string  BSAName          { get; set; } = "";
    public string  PresidentCID     { get; set; } = "";
    public string  VicePresidentCID { get; set; } = "";
    public Status    Status           { get; set; } = Status.Active;
    public Country Country          { get; set; } = null!;
    public ICollection<BSAMembership>  Memberships  { get; set; } = [];
    public ICollection<BSAFundRequest> FundRequests { get; set; } = [];
}

