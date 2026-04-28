using Microsoft.AspNetCore.Mvc;
using SPRMS.Common;

namespace SPRMS.Controllers;

/// <summary>
/// Base controller for all API endpoints.
/// Provides common functionality and consistent response formatting.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected IActionResult Success<T>(T data, string? message = null) =>
        Ok(Result<T>.Ok(data, message));

    protected IActionResult Success(string? message = null) =>
        Ok(Result.Ok(message));

    protected IActionResult NotFound<T>(string entity) =>
        StatusCode(404, Result<T>.NotFound(entity));

    protected IActionResult Forbidden<T>(string message = "Access denied.") =>
        StatusCode(403, Result<T>.Forbidden(message));

    protected IActionResult Conflict<T>(string message) =>
        StatusCode(409, Result<T>.Conflict(message));

    protected IActionResult ValidationError<T>(List<string> errors) =>
        StatusCode(400, Result<T>.ValidationFail(errors));

    protected IActionResult ValidationError(List<string> errors) =>
        StatusCode(400, Result.ValidationFail(errors));

    protected IActionResult Failed<T>(string message, string? errorCode = null) =>
        StatusCode(400, Result<T>.Fail(message, errorCode));

    protected IActionResult Failed(string message, string? errorCode = null) =>
        StatusCode(400, Result.Fail(message, errorCode));
}
