using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

public class PipelineStatusTests
{
    [Fact]
    public void Pipeline_HasFixedVocabulary()
    {
        // Guards the design-system status vocabulary (§5.2) against accidental changes.
        Assert.Equal(
            new[] { "Sourced", "Shortlisted", "SentToClient", "Interview", "Placed", "Rejected" },
            Enum.GetNames<PipelineStatus>());
    }
}
