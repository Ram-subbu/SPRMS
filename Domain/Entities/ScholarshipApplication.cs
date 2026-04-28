using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class ScholarshipApplication : SoftAuditEntity
{
    public long      ApplicationID     { get; set; }
    public long      UserID            { get; set; }
    public long      ProgramID         { get; set; }
    public long?     StudentProfileID  { get; set; }
    public string    ApplicationNumber { get; set; } = "";
    public string    PathType          { get; set; } = "Bhutan";
    public ApplicationStatus Status     { get; set; } = ApplicationStatus.Submitted;
    public string?   Class10IndexNo    { get; set; }
    public string?   Class12IndexNo    { get; set; }
    public decimal?  AcademicScore     { get; set; }
    public decimal?  InterviewScore    { get; set; }
    public decimal?  FinalMeritScore   { get; set; }
    public decimal?  FinalScore        { get; set; }
    public int?      MeritRank         { get; set; }
    public string?   OfferLetterPath   { get; set; }
    public string?   QRCode            { get; set; }
    public string?   EvaluatorComments { get; set; }
    public DateTime? SubmittedOn       { get; set; }
    public DateTime? OfferExpiresOn    { get; set; }
    public long?     RejectedBy        { get; set; }
    public DateTime? RejectedOn        { get; set; }
    public string?   RejectionReason   { get; set; }
    public User User                   { get; set; } = null!;
    public ScholarshipProgram Program  { get; set; } = null!;
    public StudentProfile? StudentProfile { get; set; }
    public ICollection<ApplicationDocument> Documents { get; set; } = [];
    public ICollection<ApplicationEvaluation> Evaluations { get; set; } = [];
}


