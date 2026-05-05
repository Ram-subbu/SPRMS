using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPRMS.API.Application.DTOs;
using SPRMS.API.Application.Interfaces;

namespace SPRMS.Controllers;

[Authorize]
public class UsersController(IUserManagementService userSvc) : BaseController
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationError(ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList());

        var userId = await userSvc.CreateUserAsync(request, ct);
        return Success(new { userId });
    }
}
