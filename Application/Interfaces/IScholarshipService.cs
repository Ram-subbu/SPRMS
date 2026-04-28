using SPRMS.Domain.Enums;

namespace SPRMS.API.Application.Interfaces;

public interface IScholarshipService
{
    bool CanPublishProgram(Status status);
}

