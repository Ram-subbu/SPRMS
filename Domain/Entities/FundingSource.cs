namespace SPRMS.API.Domain.Entities;

public class FundingSource : SoftAuditEntity
{
    public long    FundingSourceID { get; set; }
    public string  SourceName      { get; set; } = "";
    public string? Description     { get; set; }
}

