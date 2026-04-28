using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class PaymentRequest : AuditEntity
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long      PaymentID         { get => PaymentRequestID; set => PaymentRequestID = value; }
    public long      PaymentRequestID  { get; set; }
    public string?   PaymentNumber     { get; set; }
    public long?     ApplicationID     { get; set; }
    public long      ScholarshipID     { get; set; }
    public long      ExpenseTypeID     { get; set; }
    public long      RequestedByID     { get; set; }
    public long      CurrencyID        { get; set; }
    public decimal   Amount            { get; set; }
    public decimal?  AmountBTN         { get; set; }
    public DateTime? PaymentPeriodFrom { get; set; }
    public DateTime? PaymentPeriodTo   { get; set; }
    public bool      IsBulk            { get; set; }
    public Status    Status            { get; set; } = Status.PendingFO;
    public string?   PaymentMethod     { get; set; }
    public string?   AccountNumber     { get; set; }
    public string?   VoucherNumber     { get; set; }
    public string?   TransactionID     { get; set; }
    public string?   ApprovedBy        { get; set; }
    public DateTime? ApprovedOn        { get; set; }
    public string?   RejectionReason   { get; set; }
    public string?   RejectionRemarks  { get; set; }
    public string?   InvoiceFilePath   { get; set; }
    public long?     ApprovedByChiefID { get; set; }
    public DateTime? ApprovedByChiefOn { get; set; }
    public long?     ProcessedByFinID  { get; set; }
    public DateTime? ProcessedByFinOn  { get; set; }
    public long?     DisbursedByID     { get; set; }
    public DateTime? DisbursedOn       { get; set; }
    public DateTime? PaidOn            { get; set; }
    public long?     RejectedBy        { get; set; }
    public DateTime? RejectedOn        { get; set; }
    public ScholarshipApplication? Application { get; set; }
    public Scholarship Scholarship     { get; set; } = null!;
    public ExpenseType ExpenseType     { get; set; } = null!;
    public Currency    Currency        { get; set; } = null!;
}

