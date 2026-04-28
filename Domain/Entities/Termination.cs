using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class Termination : AuditEntity
{
    public long      TerminationID    { get; set; }
    public long      ScholarshipID    { get; set; }
    public string    Reason           { get; set; } = "";
    public Status    Status           { get; set; } = Status.PendingApproval;
    public decimal?  TotalSpentBTN    { get; set; }
    public decimal?  TotalSpentFCY    { get; set; }
    public long?     CurrencyID       { get; set; }
    public string    CaseStatus       { get; set; } = "Open";
    public string?   ResolutionReason { get; set; }
    public DateTime? TerminatedOn     { get; set; }
    public Scholarship   Scholarship  { get; set; } = null!;
    public RefundLedger? RefundLedger { get; set; }
}

