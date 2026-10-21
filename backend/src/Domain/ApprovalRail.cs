using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain;

/// <summary>The rules that decide an approval step — one copy, for requisitions and offers alike
/// (ADR-0030 §1). A second, simpler mechanism beside the first is how two approvals drift apart.
///
/// <list type="bullet">
/// <item>Only the highest round is live; earlier rounds are history and are never decided on
/// (ADR-0023).</item>
/// <item>A caller acts only from a waiting step they are named on. A later step outranks an earlier
/// one: approving closes every waiting step at or below the caller's, recording who actually acted
/// (ADR-0024).</item>
/// <item>Rejecting forward is refused — it would end the approval before the earlier approver ever
/// saw it. A senior may still reject at their own step once it is the active one.</item>
/// </list>
///
/// <para>The caller's own entity checks (status, reach) stay in the service, in the order the
/// service needs them: <see cref="WaitingStepOf"/> answers "is this caller a decider at all" so a
/// service can 404 an outsider <i>before</i> it reports anything about the record's state.</para>
/// </summary>
public static class ApprovalRail
{
    public enum Outcome
    {
        /// <summary>The caller's step (and any closed on others' behalf) approved; others still wait.</summary>
        StepApproved,

        /// <summary>Every step of the live round is approved.</summary>
        FullyApproved,

        /// <summary>The caller rejected at the active step.</summary>
        Rejected,
    }

    /// <summary>The steps of the live (highest) round, in chain order. Empty when nothing was ever submitted.</summary>
    public static List<T> LiveRound<T>(IEnumerable<T> steps) where T : IApprovalStep
    {
        var all = steps.ToList();
        if (all.Count == 0) return all;
        var round = all.Max(s => s.Round);
        return all.Where(s => s.Round == round).OrderBy(s => s.Sequence).ToList();
    }

    /// <summary>The round a new submission opens: one after the highest so far.</summary>
    public static int NextRound<T>(IEnumerable<T> steps) where T : IApprovalStep
        => steps.Select(s => (int?)s.Round).Max() is { } r ? r + 1 : 1;

    /// <summary>The caller's own waiting step in the live round, or null — not merely the lowest one:
    /// being further down the chain is authority to approve forward, but only from a step the caller
    /// was named on.</summary>
    public static T? WaitingStepOf<T>(IReadOnlyCollection<T> liveRound, Guid userId) where T : class, IApprovalStep
        => liveRound.FirstOrDefault(s => s.Decision == ApprovalDecision.Waiting && s.ApproverUserId == userId);

    /// <summary>Whose turn it genuinely is: the lowest undecided step of the live round.</summary>
    public static T? ActiveStep<T>(IReadOnlyCollection<T> liveRound) where T : class, IApprovalStep
        => liveRound.Where(s => s.Decision == ApprovalDecision.Waiting).MinBy(s => s.Sequence);

    /// <summary>Applies <paramref name="userId"/>'s decision from <paramref name="mine"/>.
    /// <para>Throws <see cref="InvalidOperationException"/> for a rejection from a step that is not
    /// the active one; <paramref name="subject"/> names the record in that message ("This
    /// requisition", "This offer").</para></summary>
    public static Outcome Apply<T>(
        IReadOnlyCollection<T> liveRound, T mine, Guid userId, bool approve, string? comment,
        DateTimeOffset now, string subject) where T : class, IApprovalStep
    {
        var active = ActiveStep(liveRound)
            ?? throw new InvalidOperationException($"{subject} has no step waiting for a decision.");

        if (!approve)
        {
            if (mine.Sequence != active.Sequence)
                throw new InvalidOperationException(
                    $"{subject} is waiting on {active.Label}. You can approve on their behalf, " +
                    "but only they can reject at their step.");

            mine.Decision = ApprovalDecision.Rejected;
            mine.DecidedAt = now;
            mine.Comment = comment;
            return Outcome.Rejected;
        }

        // Everything at or below the caller's own position closes in one action. When it was not
        // their step, DecidedByUserId records who really pressed the button.
        foreach (var step in liveRound.Where(s => s.Decision == ApprovalDecision.Waiting && s.Sequence <= mine.Sequence))
        {
            step.Decision = ApprovalDecision.Approved;
            step.DecidedAt = now;
            step.Comment = comment;
            if (step.ApproverUserId != userId)
                step.DecidedByUserId = userId;
        }

        // Scoped to the live round: across all rounds this could never be true once a rejected
        // round is preserved.
        return liveRound.All(s => s.Decision == ApprovalDecision.Approved) ? Outcome.FullyApproved : Outcome.StepApproved;
    }
}
