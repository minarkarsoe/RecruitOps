using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>Module 4 (ADR-0030): the offer letter's merge fields, and the approval rules offers now
/// share with requisitions.</summary>
public class OfferLetterAndApprovalRailTests
{
    private static readonly OfferLetter.Terms Terms = new(
        "Ma Ei Thandar Kyaw", "Senior Credit Analyst", "Credit Risk", "Yoma Bank",
        480_000m, 45_000m, new DateOnly(2026, 10, 1), 6, new DateOnly(2026, 8, 25));

    [Fact]
    public void The_Letter_Is_Filled_From_The_Terms_With_Readable_Numbers_And_Dates()
    {
        var text = OfferLetter.Render(
            "{{CandidateName}}: MMK {{BaseSalary}} + {{Allowances}} = {{TotalPackage}}, from {{JoiningDate}}, until {{ExpiresOn}}.",
            Terms);

        Assert.Equal("Ma Ei Thandar Kyaw: MMK 480,000 + 45,000 = 525,000, from 1 October 2026, until 25 August 2026.", text);
    }

    [Fact]
    public void A_Mistyped_Field_Stays_Visible_Instead_Of_Becoming_Blank()
    {
        Assert.Equal("Salary: {{BaseSalry}}", OfferLetter.Render("Salary: {{BaseSalry}}", Terms));
        Assert.Equal("Dear Ma Ei Thandar Kyaw", OfferLetter.Render("Dear {{ CandidateName }}", Terms));
    }

    [Fact]
    public void A_Name_From_A_Form_Cannot_Add_Paragraphs_To_The_Contract()
    {
        var crafted = Terms with { CandidateName = "Ko Ko\n\nYou will also receive a car.\r\u2028" };
        Assert.Equal("Dear Ko Ko You will also receive a car.,", OfferLetter.Render("Dear {{CandidateName}},", crafted));
    }

    [Fact]
    public void The_Default_Letter_Uses_Only_Fields_The_Renderer_Knows()
    {
        var rendered = OfferLetter.Render(OfferLetter.DefaultBody, Terms);
        Assert.DoesNotContain("{{", rendered);
    }

    [Fact]
    public void A_New_Submission_Opens_The_Next_Round_And_Only_It_Is_Live()
    {
        var steps = new List<OfferApproval>
        {
            new() { Round = 1, Sequence = 1, Decision = ApprovalDecision.Rejected },
            new() { Round = 2, Sequence = 1, Decision = ApprovalDecision.Waiting },
        };

        Assert.Equal(1, ApprovalRail.NextRound(new List<OfferApproval>()));
        Assert.Equal(3, ApprovalRail.NextRound(steps));
        Assert.All(ApprovalRail.LiveRound(steps), s => Assert.Equal(2, s.Round));
    }

    [Fact]
    public void Approving_Forward_Records_Who_Acted_And_Rejecting_Forward_Is_Refused()
    {
        var junior = Guid.NewGuid();
        var senior = Guid.NewGuid();
        var round = new List<OfferApproval>
        {
            new() { Round = 1, Sequence = 1, ApproverUserId = junior, Label = "Finance Controller" },
            new() { Round = 1, Sequence = 2, ApproverUserId = senior, Label = "Chief HR Officer" },
        };
        var mine = ApprovalRail.WaitingStepOf(round, senior)!;
        var now = DateTimeOffset.UtcNow;

        var refused = Assert.Throws<InvalidOperationException>(
            () => ApprovalRail.Apply(round, mine, senior, approve: false, null, now, "This offer"));
        Assert.Contains("waiting on Finance Controller", refused.Message);

        Assert.Equal(ApprovalRail.Outcome.FullyApproved, ApprovalRail.Apply(round, mine, senior, approve: true, "ok", now, "This offer"));
        Assert.Equal(senior, round[0].DecidedByUserId); // closed on the junior's behalf
        Assert.Null(round[1].DecidedByUserId);           // the senior's own step
    }
}
