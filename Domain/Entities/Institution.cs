namespace SPRMS.API.Domain.Entities;

public class Institution : SoftAuditEntity
{
    public long    InstitutionID   { get; set; }
    public long    CountryID       { get; set; }
    public string  InstitutionName { get; set; } = "";
    public string  InstitutionType { get; set; } = "Public";
    public string? HODEmail        { get; set; }
    public string? BankAcctHolder  { get; set; }
    public string? BankName        { get; set; }
    public string? AccountNumber   { get; set; }
    public string? IFSCSwiftCode   { get; set; }
    public Country Country         { get; set; } = null!;
}

