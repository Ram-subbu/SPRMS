using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class Scholarship : SoftAuditEntity
{
    public long      ScholarshipID    { get; set; }
    public long      StudentProfileID { get; set; }
    public long      ProgramID        { get; set; }
    public long      InstitutionID    { get; set; }
    public string    CourseName       { get; set; } = "";
    public string    ScholarshipType  { get; set; } = "InService";
    public Status    Status           { get; set; } = Status.Active;
    public DateTime  StartDate        { get; set; }
    public DateTime  OriginalEndDate  { get; set; }
    public DateTime  CurrentEndDate   { get; set; }
    public decimal?  ObligationYears  { get; set; }
    public bool      LegacyFlag       { get; set; }
    public decimal?  HistoricalExpBTN { get; set; }
    public StudentProfile StudentProfile    { get; set; } = null!;
    public ScholarshipProgram Program       { get; set; } = null!;
    public Institution      Institution     { get; set; } = null!;
    public ICollection<PaymentRequest>   PaymentRequests   { get; set; } = [];
    public ICollection<ProgressReport>   ProgressReports   { get; set; } = [];
    public ICollection<ExtensionRequest> ExtensionRequests { get; set; } = [];
    public Termination?                  Termination       { get; set; }
}

