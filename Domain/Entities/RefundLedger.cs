using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class RefundLedger : AuditEntity
{
    public long      RefundLedgerID     { get; set; }
    public long      TerminationID      { get; set; }
    public decimal   TotalOwed          { get; set; }
    public decimal   AmountPaid         { get; set; }
    public decimal   OutstandingBalance { get; set; }
    public DateTime  DueDate            { get; set; }
    public Status    Status             { get; set; } = Status.AwaitingPayment;
    public Termination Termination      { get; set; } = null!;
    public ICollection<RefundPayment> Payments { get; set; } = [];
}

