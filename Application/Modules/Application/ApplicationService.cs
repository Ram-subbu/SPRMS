using System.Threading.Tasks;
using SPRMS.API.Application.Interfaces;
using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using SPRMS.API.Application.DTOs;
using SPRMS.API.Common;
using SPRMS.Domain.Enums;
using System.Security.Claims;

namespace SPRMS.API.Application.Modules.Application
{
    public class ApplicationService : IApplicationService
    {
        private readonly AppDbContext _context;

        public ApplicationService(AppDbContext context)
        {
            _context = context;
        }

        public static object FromClaims(ClaimsPrincipal user)
        {
            // TODO: Implement proper user extraction
            return new { UserID = 1L };
        }

        public async Task<long> ApplyAsync(long userId, long programId)
        {
            var application = new ScholarshipApplication
            {
                UserID = userId,
                ProgramID = programId,
                Status = ApplicationStatus.Submitted,
                ApplicationNumber = System.Guid.NewGuid().ToString(),
                SubmittedOn = DateTime.UtcNow,
                IsActive = true
            };

            _context.ScholarshipApplications.Add(application);
            await _context.SaveChangesAsync();

            return application.ApplicationID;
        }

        public Task<ModuleResponse> EvaluateAsync(long id, EvaluateApplicationRequest request, object user, CancellationToken ct)
        {
            // TODO: Implement evaluation logic
            return Task.FromResult(new ModuleResponse { Success = true, StatusCode = 200 });
        }

        public Task<ModuleResponse> GetStatsAsync(CancellationToken ct)
        {
            // TODO: Implement stats logic
            return Task.FromResult(new ModuleResponse { Success = true, StatusCode = 200 });
        }

        public Task<ModuleResponse> GetApplicationsAsync(int page, int pageSize, string? status, long? programId, object user, CancellationToken ct)
        {
            // TODO: Implement get applications logic
            return Task.FromResult(new ModuleResponse { Success = true, StatusCode = 200 });
        }

        public Task<ModuleResponse> GetApplicationAsync(long id, object user, CancellationToken ct)
        {
            // TODO: Implement get application logic
            return Task.FromResult(new ModuleResponse { Success = true, StatusCode = 200 });
        }

        public Task<ModuleResponse> SubmitApplicationAsync(SubmitApplicationRequest request, object user, CancellationToken ct)
        {
            // TODO: Implement submit logic
            return Task.FromResult(new ModuleResponse { Success = true, StatusCode = 201 });
        }

        public Task<ModuleResponse> UpdateStatusAsync(long id, UpdateApplicationStatusRequest request, object user, CancellationToken ct)
        {
            // TODO: Implement update status logic
            return Task.FromResult(new ModuleResponse { Success = true, StatusCode = 200 });
        }
    }
}