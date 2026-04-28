using Microsoft.AspNetCore.Mvc;
using SPRMS.API.Application.Interfaces;

namespace SPRMS.API.Controllers
{
    [ApiController]
    [Route("api/applications")]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _service;

        public ApplicationsController(IApplicationService service)
        {
            _service = service;
        }

        [HttpPost("apply")]
        public async Task<IActionResult> Apply(long userId, long programId)
        {
            var id = await _service.ApplyAsync(userId, programId);
            return Ok(new { ApplicationID = id });
        }
    }
}