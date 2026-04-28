namespace SPRMS.API.Domain.Entities;

public class StudentProfile : SoftAuditEntity
{
    public long      StudentProfileID { get; set; }
    public long      UserID           { get; set; }
    public long      ApplicationID    { get; set; }
    public string?   Stream           { get; set; }
    public DateTime? DOB              { get; set; }
    public string?   Gender           { get; set; }
    public string?   FatherName       { get; set; }
    public string?   MotherName       { get; set; }
    public string?   GuardianName     { get; set; }
    public string?   GuardianRelation { get; set; }
    public string?   PermanentAddress { get; set; }
    public string?   BankAcctNumber   { get; set; }
    public string?   BankName         { get; set; }
    public string?   SwiftCode        { get; set; }
    public string    ProfileStatus    { get; set; } = "AwaitingPlacement";
    public User      User             { get; set; } = null!;
    public ScholarshipApplication Application { get; set; } = null!;
    public ICollection<Scholarship> Scholarships { get; set; } = [];
    public ICollection<Education>   Educations   { get; set; } = [];
}

