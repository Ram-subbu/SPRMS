using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class ProgressReport : AuditEntity
{
    public long      ReportID        { get; set; }
    public long      ScholarshipID   { get; set; }
    public string    ReportingPeriod { get; set; } = "";
    public DateTime  DueDate         { get; set; }
    public DateTime? SubmittedOn     { get; set; }
    public Status    Status          { get; set; } = Status.Pending;
    public string?   FilePath        { get; set; }
    public string?   StudentRemarks  { get; set; }
    public string?   FORemarks       { get; set; }
    public bool      StipendUnlocked { get; set; }
    public long?     RejectedBy      { get; set; }
    public DateTime? RejectedOn      { get; set; }
    public string?   RejectionReason { get; set; }
    public Scholarship Scholarship   { get; set; } = null!;
}

