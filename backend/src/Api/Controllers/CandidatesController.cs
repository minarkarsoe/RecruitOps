using Microsoft.AspNetCore.Mvc;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CandidatesController : ControllerBase
{
    // TODO: inject ICandidateService and implement endpoints.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());
}
