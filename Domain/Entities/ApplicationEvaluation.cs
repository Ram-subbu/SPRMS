namespace SPRMS.API.Domain.Entities;

public class ApplicationEvaluation : AuditEntity
{
    public long      EvaluationID   { get; set; }
    public long      ApplicationID  { get; set; }
    public string    EvaluatorName  { get; set; } = "";
    public decimal   AcademicScore  { get; set; }
    public decimal   InterviewScore { get; set; }
    public decimal   FinancialScore { get; set; }
    public string?   Comments       { get; set; }
    public DateTime  EvaluatedOn    { get; set; } = DateTime.UtcNow;
    public ScholarshipApplication Application { get; set; } = null!;
}

