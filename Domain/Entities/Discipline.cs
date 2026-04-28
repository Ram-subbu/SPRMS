namespace SPRMS.API.Domain.Entities;

public class Discipline : AuditEntity
{
    public long    DisciplineID   { get; set; }
    public string  DisciplineName { get; set; } = "";
    public string? Description    { get; set; }
    public bool    IsActive       { get; set; } = true;
    public ICollection<Subject> Subjects { get; set; } = [];
}

