namespace SPRMS.API.Domain.Entities;

public class Subject : AuditEntity
{
    public long    SubjectID    { get; set; }
    public long    DisciplineID { get; set; }
    public string  SubjectName  { get; set; } = "";
    public string? Description  { get; set; }
    public bool    IsActive     { get; set; } = true;
    public Discipline Discipline { get; set; } = null!;
}

