using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // public job page for applicants (Module 2.1); resolved from link token (TODO)
public class PortalController : ControllerBase
{
    // TODO: inject IPortalService and implement endpoints.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());
}
