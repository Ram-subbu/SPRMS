using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class ScholarshipProgram : SoftAuditEntity
{
    public long      ProgramID          { get; set; }
    public long      FundingSourceID    { get; set; }
    public string    ProgramName        { get; set; } = "";
    public string?   Description        { get; set; }
    public string    ScholarshipType    { get; set; } = "InService";
    public int       AvailableSlots     { get; set; }
    public string?   EligibilityRules   { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? StartDate          { get; set; }
    public DateTime? EndDate            { get; set; }
    public int       MaxApplications    { get; set; }
    public decimal   AwardPerStudent    { get; set; }
    public decimal   TotalAward         { get; set; }
    public decimal?  MinGPA             { get; set; }
    public int       TakenApplications  { get; set; }
    public Status    Status             { get; set; } = Status.Active;
    public FundingSource FundingSource   { get; set; } = null!;
    public ICollection<ScholarshipApplication> Applications { get; set; } = [];
}

