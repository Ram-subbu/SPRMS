using SPRMS.Domain.Enums;
using SPRMS.API.Application.Interfaces;

namespace SPRMS.Application.Modules.Scholarship;

public class ScholarshipService : IScholarshipService
{
    public bool CanPublishProgram(Status status)
    {
        return status is Status.Active or Status.Closed;
    }
}



