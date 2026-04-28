using Microsoft.AspNetCore.Authorization;
using SPRMS.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using SPRMS.API.Application.Modules.Application;
using SPRMS.API.Application.DTOs;
using SPRMS.API.Common;

namespace SPRMS.Controllers;

/// <summary>
/// Scholarship Applications management endpoints.
/// Students submit applications, staff reviews and processes them.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class ApplicationsController(IApplicationService applicationService) : BaseController
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] long? programId = null,
        CancellationToken ct = default)
    {
        var user = ApplicationService.FromClaims(User);
        var result = await applicationService.GetApplicationsAsync(page, pageSize, status, programId, user, ct);
        return ToActionResult(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplication(long id, CancellationToken ct = default)
    {
        var user = ApplicationService.FromClaims(User);
        var result = await applicationService.GetApplicationAsync(id, user, ct);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitApplication(
        [FromBody] SubmitApplicationRequest request,
        CancellationToken ct = default)
    {
        var user = ApplicationService.FromClaims(User);
        var result = await applicationService.SubmitApplicationAsync(request, user, ct);
        return ToActionResult(result);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Finance,Evaluator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateApplicationStatus(
        long id,
        [FromBody] UpdateApplicationStatusRequest request,
        CancellationToken ct = default)
    {
        var user = ApplicationService.FromClaims(User);
        var result = await applicationService.UpdateStatusAsync(id, request, user, ct);
        return ToActionResult(result);
    }

    [HttpPost("{id}/evaluate")]
    [Authorize(Roles = "Admin,Finance,Evaluator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EvaluateApplication(
        long id,
        [FromBody] EvaluateApplicationRequest request,
        CancellationToken ct = default)
    {
        var user = ApplicationService.FromClaims(User);
        var result = await applicationService.EvaluateAsync(id, request, user, ct);
        return ToActionResult(result);
    }

    [HttpGet("stats/summary")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplicationStats(CancellationToken ct = default)
    {
        var result = await applicationService.GetStatsAsync(ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(ModuleResponse result)
    {
        if (result.Success && result.StatusCode == StatusCodes.Status201Created)
        {
            return Created(result.Location ?? string.Empty, result.Data);
        }

        if (result.Success)
        {
            return Success(result.Data ?? new { });
        }

        if (result.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound(new { message = result.Message });
        }

        if (result.Errors?.Count > 0)
        {
            return ValidationError(result.Errors.ToList());
        }

        return Failed(result.Message ?? "Request failed");
    }
}



