using Microsoft.AspNetCore.Mvc;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    // TODO: inject IClientService and implement endpoints.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());
}
