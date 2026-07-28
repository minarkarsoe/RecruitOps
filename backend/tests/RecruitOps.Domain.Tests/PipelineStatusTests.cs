using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

public class PipelineStatusTests
{
    [Fact]
    public void Pipeline_HasFixedInHouseVocabulary()
    {
        // Guards the design-system status vocabulary (§5.2) against accidental drift.
        // Must stay in sync with frontend lib/types.ts.
        Assert.Equal(
            new[] { "Sourced", "Applied", "Screening", "Shortlisted",
                    "Interview", "Offer", "Hired", "Rejected" },
            Enum.GetNames<PipelineStatus>());
    }

    [Fact]
    public void Roles_MatchInHouseModel()
    {
        // No external "Client" role in the in-house model (ADR-0001).
        Assert.Equal(
            new[] { "Admin", "HrDirector", "Recruiter", "HiringManager", "Approver" },
            Enum.GetNames<UserRole>());
    }
}
