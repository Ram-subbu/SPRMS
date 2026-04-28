namespace SPRMS.API.Domain.Entities;

public class Currency : AuditEntity
{
    public long   CurrencyID   { get; set; }
    public string CurrencyName { get; set; } = "";
    public string Symbol       { get; set; } = "";
    public string ISOCode      { get; set; } = "";
    public bool   IsActive     { get; set; } = true;
}

