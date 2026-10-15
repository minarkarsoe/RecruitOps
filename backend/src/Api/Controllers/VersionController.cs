using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Health;
using RecruitOps.Application.DTOs;

namespace RecruitOps.Api.Controllers;

/// <summary>Build identity for support and the version registry (ADR-0004).
/// <para><b>Signed-in users only</b>, unlike <c>/health/*</c>: an exact version and commit tell
/// an anonymous scanner which known vulnerabilities to try. Liveness and readiness carry no
/// version for the same reason.</para></summary>
[ApiController]
[Route("api/version")]
[Authorize]
public class VersionController : ControllerBase
{
    [HttpGet]
    public ActionResult<VersionDto> Get([FromServices] IHostEnvironment env) =>
        Ok(new VersionDto(BuildInfo.Version, BuildInfo.Commit, env.EnvironmentName));
}
