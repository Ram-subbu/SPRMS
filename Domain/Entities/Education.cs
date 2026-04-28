namespace SPRMS.API.Domain.Entities;

public class Education
{
    public long      EducationID      { get; set; }
    public long?     StudentProfileID { get; set; }
    public string?   Qualification    { get; set; }
    public string?   Institute        { get; set; }
    public string?   DegreeType       { get; set; }
    public string?   Subject          { get; set; }
    public decimal?  Score            { get; set; }
    public string?   Country          { get; set; }
    public string?   Funding          { get; set; }
    public DateTime? StartDate        { get; set; }
    public DateTime? EndDate          { get; set; }
    public bool      IsActive         { get; set; } = true;
    public long      EntryBy          { get; set; }
    public DateTime  EntryDate        { get; set; } = DateTime.UtcNow;
    public StudentProfile? StudentProfile { get; set; }
}

