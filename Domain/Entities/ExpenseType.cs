namespace SPRMS.API.Domain.Entities;

public class ExpenseType : AuditEntity
{
    public long   ExpenseTypeID  { get; set; }
    public string CategoryName   { get; set; } = "";
    public string TransferMethod { get; set; } = "Direct";
    public bool   IsActive       { get; set; } = true;
}

