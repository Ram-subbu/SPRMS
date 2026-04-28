namespace SPRMS.API.Domain.Entities;

public class RefundPayment : AuditEntity
{
    public long      RefundPaymentID { get; set; }
    public long      RefundLedgerID  { get; set; }
    public long      RecordedByID    { get; set; }
    public decimal   AmountPaid      { get; set; }
    public DateTime  DatePaid        { get; set; }
    public string    ProofFilePath   { get; set; } = "";
    public string?   Notes           { get; set; }
    public RefundLedger RefundLedger { get; set; } = null!;
}

