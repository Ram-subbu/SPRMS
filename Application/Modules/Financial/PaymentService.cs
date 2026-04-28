using SPRMS.Domain.Enums;
using SPRMS.API.Application.Interfaces;

namespace SPRMS.Application.Modules.Financial;

public class PaymentService : IPaymentService
{
    public bool CanApprove(Status status)
    {
        return status == Status.Pending;
    }

    public bool CanDisburse(Status status)
    {
        return status == Status.Approved;
    }
}



