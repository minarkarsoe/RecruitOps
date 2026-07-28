using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Controllers;

/// <summary>User directory. Two endpoints, two policies, and the policies are declared
/// <b>per action</b> — never at the class level.
///
/// <para><b>Why that is not a style preference.</b> ASP.NET Core authorization attributes are
/// <b>additive</b>: an action-level <c>[Authorize]</c> does not replace a class-level one, it is
/// evaluated <i>in addition</i> to it. This class previously carried
/// <c>[Authorize(Policy = AdminOnly)]</c> with <c>[Authorize(Policy = RecruitmentStaff)]</c> on
/// <see cref="Selectable"/>, intending to "opt down" — the actual effect was
/// <c>AdminOnly</c> <b>AND</b> <c>RecruitmentStaff</c>, so the endpoint was reachable only by an
/// Admin and a Recruiter got 403. That is the exact opposite of ADR-0019, whose entire purpose
/// is that a Recruiter can name an interview panel, and it made the Module 3 scheduling flow
/// undrivable by the role it was opened to — twice over, for the same underlying reason.</para>
///
/// <para><b>There is no way to widen a policy from an action.</b> If a future endpoint here
/// needs a weaker requirement than its neighbours, add it as another per-action attribute; do
/// not reintroduce a class-level policy and try to override it.</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    /// <summary>All active users in the tenant, ordered by display name.
    /// Intentionally excludes PasswordHash and other sensitive fields.
    ///
    /// <para><c>AdminOnly</c> is declared here rather than on the class: the full directory
    /// carries email addresses and exists for the approval-chain builder, where picking an
    /// approver is an Admin task. Moving this up to the class would silently re-apply it to
    /// <see cref="Selectable"/> as well — see the class remarks.</para></summary>
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> Get(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
            .ToListAsync(ct);

        return Ok(users);
    }

    /// <summary>Active users who can be put on an interview panel, ordered by display name.
    ///
    /// <para><b>Why this is not just <see cref="Get"/> with a wider policy.</b> Scheduling an
    /// interview is <c>RecruitmentStaff</c> work (ADR-0017) and the panel is required and
    /// non-empty, so a Recruiter who cannot list users cannot schedule at all — they would be
    /// pasting GUIDs. But the existing directory hands out every email address in the company,
    /// and "the picker needs names" is not a reason to publish those. This returns id, name and
    /// role only, so the wider audience gets a narrower payload.</para>
    ///
    /// <para><b>Approvers are deliberately included.</b> ADR-0018 removed their standing reach
    /// into candidate data, and it is tempting to read that as "an Approver may not sit on a
    /// panel" — it says the opposite. Panel membership is exactly how an excluded role reaches
    /// one application, the same route a Hiring Manager from another department takes
    /// (ADR-0017 §4). Filtering them out here would quietly remove that.</para>
    ///
    /// <para>Not department-scoped: a panel routinely crosses departments (a Finance
    /// interviewer on a Sales hire), and participation is what grants that person their
    /// read — it is not a leak of anything but names already visible across the company.</para>
    /// </summary>
    [HttpGet("selectable")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<IReadOnlyList<SelectableUserDto>>> Selectable(
        CancellationToken ct)
    {
        // Two-step (query in SQL, project in memory) — EF Core 10 will not translate
        // `enum.ToString()` into SQL, so the ToString happens here, after materialisation.
        // `Get` above projects the enum inside the query and has never been run against
        // Postgres; do not copy that shape.
        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.Role })
            .ToListAsync(ct);

        var users = rows
            .Select(u => new SelectableUserDto(u.Id, u.DisplayName, u.Role.ToString()))
            .ToList();

        return Ok(users);
    }
}
