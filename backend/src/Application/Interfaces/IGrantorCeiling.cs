using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.Interfaces;

/// <summary>Nobody grants what they do not hold (ADR-0021 option A, decision 4).
/// <para>Both methods judge the signed-in user, read from the database, and throw
/// <see cref="UnauthorizedAccessException"/> — a 403 — on refusal, including when there is no
/// signed-in user to judge. SuperAdmin passes both.</para></summary>
public interface IGrantorCeiling
{
    /// <summary>Giving someone <paramref name="holderRole"/> as their enum, and the
    /// <paramref name="permissionCodes"/> being added: the actor must cover the enum
    /// (<c>RoleScope.Covers</c>) and hold every one of the permissions.</summary>
    Task EnsureCanGrantAsync(UserRole holderRole, IEnumerable<string> permissionCodes, CancellationToken ct = default);

    /// <summary>Changing a user or role whose current reach is <paramref name="currentRole"/> —
    /// demoting, deactivating, editing. Not a grant, but managing someone you do not outrank.</summary>
    Task EnsureCanManageAsync(UserRole currentRole, CancellationToken ct = default);
}
