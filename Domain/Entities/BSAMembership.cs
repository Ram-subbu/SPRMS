using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class BSAMembership : AuditEntity
{
    public long   MembershipID { get; set; }
    public long   BSAID        { get; set; }
    public long   UserID       { get; set; }
    public Status    Status       { get; set; } = Status.Pending;
    public BSAAssociation BSA  { get; set; } = null!;
    public User   User         { get; set; } = null!;
}

