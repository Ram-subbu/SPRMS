using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class ExtensionRequest : AuditEntity
{
    public long      ExtensionRequestID   { get; set; }
    public long      ScholarshipID        { get; set; }
    public long      ReasonID             { get; set; }
    public DateTime  NewEndDate           { get; set; }
    public string?   FundingType          { get; set; }
    public Status    Status               { get; set; } = Status.PendingDecision;
    public string    UniversityLetterPath { get; set; } = "";
    public string?   SupportingDocPaths   { get; set; }
    public string?   FORemarks            { get; set; }
    public long?     ApprovedBy           { get; set; }
    public DateTime? ApprovedOn           { get; set; }
    public long?     RejectedBy           { get; set; }
    public DateTime? RejectedOn           { get; set; }
    public string?   RejectionReason      { get; set; }
    public Scholarship    Scholarship     { get; set; } = null!;
    public ExtensionReason Reason         { get; set; } = null!;
}

