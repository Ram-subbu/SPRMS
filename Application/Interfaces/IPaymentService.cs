using SPRMS.Domain.Enums;

namespace SPRMS.API.Application.Interfaces;

public interface IPaymentService
{
    bool CanApprove(Status status);
    bool CanDisburse(Status status);
}

