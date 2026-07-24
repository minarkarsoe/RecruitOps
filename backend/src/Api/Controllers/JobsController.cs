using Microsoft.AspNetCore.Mvc;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    // TODO: inject IJobService and implement endpoints.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());
}
