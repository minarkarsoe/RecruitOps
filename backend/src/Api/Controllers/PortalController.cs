using Microsoft.AspNetCore.Mvc;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortalController : ControllerBase
{
    // TODO: inject IPortalService and implement endpoints.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());
}
