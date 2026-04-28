using System.Threading.Tasks;
using SPRMS.API.Application.DTOs;
using SPRMS.API.Common;

namespace SPRMS.API.Application.Interfaces
{
    public interface IApplicationService
    {
        Task<long> ApplyAsync(long userId, long programId);
        Task<ModuleResponse> GetApplicationsAsync(int page, int pageSize, string? status, long? programId, object user, CancellationToken ct);
        Task<ModuleResponse> GetApplicationAsync(long id, object user, CancellationToken ct);
        Task<ModuleResponse> SubmitApplicationAsync(SubmitApplicationRequest request, object user, CancellationToken ct);
        Task<ModuleResponse> UpdateStatusAsync(long id, UpdateApplicationStatusRequest request, object user, CancellationToken ct);
        Task<ModuleResponse> EvaluateAsync(long id, EvaluateApplicationRequest request, object user, CancellationToken ct);
        Task<ModuleResponse> GetStatsAsync(CancellationToken ct);
    }
}