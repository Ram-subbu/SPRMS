using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPRMS.Common;
using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SPRMS.Domain.Enums;

namespace SPRMS.Controllers;

/// <summary>
/// Scholarship Program management endpoints.
/// CRUD operations for defining scholarship programs, eligibility criteria, and benefits.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class ScholarshipsController(AppDbContext db, ILogChannel log) : BaseController
{
    /// <summary>
    /// Get all active scholarship programs with pagination.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (default 20, max 100)</param>
    /// <param name="status">Filter by status (Active, Closed, Archived)</param>
    /// <returns>Paginated list of programs</returns>
    [HttpGet]
    [AllowAnonymous]  // Public listing
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrograms(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);  // Max 100 per page

            var query = db.ScholarshipPrograms.AsQueryable();

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Status>(status, out var statusEnum))
                query = query.Where(p => p.Status == statusEnum);

            // Only show active programs unless admin
            if (!User.IsInRole("Admin"))
                query = query.Where(p => p.Status == Status.Active);

            var total = await query.CountAsync(ct);
            var programs = await query
                .OrderByDescending(p => p.CreatedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.ProgramID,
                    p.ProgramName,
                    p.Description,
                    p.MaxApplications,
                    p.TotalAward,
                    p.AwardPerStudent,
                    p.MinGPA,
                    p.TakenApplications,
                    p.Status,
                    Progress = p.TakenApplications > 0 ? (int)((p.TakenApplications * 100) / p.MaxApplications) : 0,
                    p.CreatedOn
                })
                .ToListAsync(ct);

            return Success(new
            {
                data = programs,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    pages = (int)Math.Ceiling(total / (decimal)pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Scholarship", FunctionName: nameof(GetPrograms),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to retrieve programs");
        }
    }

    /// <summary>
    /// Get detailed view of a single scholarship program.
    /// </summary>
    /// <param name="id">Program ID</param>
    /// <returns>Program details including eligibility and benefits</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProgram(long id, CancellationToken ct = default)
    {
        try
        {
            var program = await db.ScholarshipPrograms
                .FirstOrDefaultAsync(p => p.ProgramID == id, ct);

            if (program == null)
                return NotFound(new { message = "Program not found" });

            // Check visibility
            if (program.Status != Status.Active && !User.IsInRole("Admin"))
                return NotFound(new { message = "Program not found" });

            var result = new
            {
                program.ProgramID,
                program.ProgramName,
                program.Description,
                program.ApplicationDeadline,
                program.StartDate,
                program.EndDate,
                program.MaxApplications,
                program.TotalAward,
                program.AwardPerStudent,
                program.MinGPA,
                program.TakenApplications,
                program.Status,
                program.EligibilityRules
            };

            return Success(result);
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Scholarship", FunctionName: nameof(GetProgram),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to retrieve program");
        }
    }

    /// <summary>
    /// Create a new scholarship program (Admin only).
    /// </summary>
    /// <param name="request">Program details</param>
    /// <returns>Created program with ID</returns>
    [HttpPost]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateProgram(
        [FromBody] CreateProgramRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.ProgramName))
                return ValidationError(new List<string> { "Program name is required" });

            if (request.MaxApplications <= 0 || request.AwardPerStudent <= 0)
                return ValidationError(new List<string> { "Max applications and award must be positive" });

            var program = new ScholarshipProgram
            {
                ProgramName = request.ProgramName,
                Description = request.Description,
                ApplicationDeadline = request.ApplicationDeadline,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                MaxApplications = request.MaxApplications,
                TotalAward = request.MaxApplications * request.AwardPerStudent,
                AwardPerStudent = request.AwardPerStudent,
                MinGPA = request.MinGPA ?? 3.0m,
                Status = Status.Active,
                TakenApplications = 0,
                CreatedBy = User.FindFirst("uid")?.Value ?? "system",
                CreatedOn = DateTime.UtcNow
            };

            db.ScholarshipPrograms.Add(program);
            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "SCHOLARSHIP_PROGRAM_CREATED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"New program created: {request.ProgramName}", Outcome: "Success"));

            return Created($"api/v1/scholarships/{program.ProgramID}", new
            {
                program.ProgramID,
                program.ProgramName,
                program.Status
            });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Scholarship", FunctionName: nameof(CreateProgram),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to create program");
        }
    }

    /// <summary>
    /// Update scholarship program details (Admin only).
    /// </summary>
    /// <param name="id">Program ID</param>
    /// <param name="request">Updated program details</param>
    /// <returns>Updated program</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProgram(
        long id,
        [FromBody] UpdateProgramRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var program = await db.ScholarshipPrograms.FindAsync(new object[] { id }, cancellationToken: ct);
            if (program == null)
                return NotFound(new { message = "Program not found" });

            // Update only provided fields
            if (!string.IsNullOrWhiteSpace(request.ProgramName))
                program.ProgramName = request.ProgramName;

            if (!string.IsNullOrWhiteSpace(request.Description))
                program.Description = request.Description;

            if (request.ApplicationDeadline.HasValue)
                program.ApplicationDeadline = request.ApplicationDeadline.Value;

            if (request.StartDate.HasValue)
                program.StartDate = request.StartDate.Value;

            if (request.EndDate.HasValue)
                program.EndDate = request.EndDate.Value;

            if (request.Status.HasValue)
                program.Status = request.Status.Value;

            if (request.MinGPA.HasValue)
                program.MinGPA = request.MinGPA.Value;

            program.UpdatedBy = User.FindFirst("uid")?.Value ?? "system";
            program.UpdatedOn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "SCHOLARSHIP_PROGRAM_UPDATED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"Program updated: {program.ProgramName}", Outcome: "Success"));

            return Success(new { program.ProgramID, program.ProgramName, program.Status });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Scholarship", FunctionName: nameof(UpdateProgram),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to update program");
        }
    }

    /// <summary>
    /// Delete scholarship program (Admin only).
    /// </summary>
    /// <param name="id">Program ID</param>
    /// <returns>Deletion confirmation</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProgram(long id, CancellationToken ct = default)
    {
        try
        {
            var program = await db.ScholarshipPrograms
                .Include(p => p.Applications)
                .FirstOrDefaultAsync(p => p.ProgramID == id, ct);

            if (program == null)
                return NotFound(new { message = "Program not found" });

            // Prevent deletion if applications exist
            if (program.Applications?.Any() == true)
                return ValidationError(new List<string> { "Cannot delete program with existing applications" });

            db.ScholarshipPrograms.Remove(program);
            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "SCHOLARSHIP_PROGRAM_DELETED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"Program deleted: {program.ProgramName}", Outcome: "Success"));

            return Success(new { message = "Program deleted successfully" });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Scholarship", FunctionName: nameof(DeleteProgram),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to delete program");
        }
    }
}

// ── Request/Response Models ────────────────────────────────────

public record CreateProgramRequest(
    string ProgramName,
    string? Description,
    DateTime ApplicationDeadline,
    DateTime StartDate,
    DateTime EndDate,
    int MaxApplications,
    decimal AwardPerStudent,
    decimal? MinGPA = null
);

public record UpdateProgramRequest(
    string? ProgramName,
    string? Description,
    DateTime? ApplicationDeadline,
    DateTime? StartDate,
    DateTime? EndDate,
    Status? Status,
    decimal? MinGPA
);
