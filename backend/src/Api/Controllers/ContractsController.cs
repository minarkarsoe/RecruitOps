using Microsoft.AspNetCore.Mvc;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContractsController : ControllerBase
{
    // TODO: inject IContractService and implement endpoints.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());
}
