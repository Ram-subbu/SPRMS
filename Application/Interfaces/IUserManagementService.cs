using SPRMS.API.Application.DTOs;

namespace SPRMS.API.Application.Interfaces;

public interface IUserManagementService
{
    Task<long> CreateUserAsync(UserCreateRequest request, CancellationToken ct = default);
}
