using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 3.3 — evaluations, and the blind-scoring rule (ADR-0017 §3).</summary>
public class ScorecardService : IScorecardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IApplicationAccess _access;
    private readonly TimeProvider _clock;

    public ScorecardService(
        AppDbContext db, ICurrentUser user, IApplicationAccess access, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _access = access;
        _clock = clock;
    }

    public async Task<MyScorecardDto?> GetMineAsync(
        Guid interviewId, CancellationToken ct = default)
    {
        var interview = await LoadForParticipantAsync(interviewId, ct);
        if (interview is null) return null;

        var existing = await LoadMyScorecardAsync(interviewId, ct);
        return await BuildMineAsync(interview, existing, ct);
    }

    public async Task<MyScorecardDto?> SaveMineAsync(
        Guid interviewId, SaveScorecardRequest request, CancellationToken ct = default)
        => await WriteMineAsync(interviewId, request, submit: false, ct);

    public async Task<MyScorecardDto?> SubmitMineAsync(
        Guid interviewId, SaveScorecardRequest request, CancellationToken ct = default)
        => await WriteMineAsync(interviewId, request, submit: true, ct);

    private async Task<MyScorecardDto?> WriteMineAsync(
        Guid interviewId, SaveScorecardRequest request, bool submit, CancellationToken ct)
    {
        var interview = await LoadForParticipantAsync(interviewId, ct);
        if (interview is null) return null;

        var userId = _user.UserId!.Value; // non-null: LoadForParticipantAsync proved it

        if (interview.Status == InterviewStatus.Cancelled)
            throw new InvalidOperationException(
                "This round was cancelled; there is nothing to evaluate.");

        var scorecard = await _db.Scorecards
            .FirstOrDefaultAsync(s => s.InterviewId == interviewId
                                      && s.InterviewerUserId == userId, ct);

        // Submitting is one-way. An evaluation that could be revised after reading the
        // panel's is not blind at all — it just delays the anchoring by one request.
        if (scorecard is not null && scorecard.Status == ScorecardStatus.Submitted)
            throw new InvalidOperationException(
                "You have already submitted this scorecard; it cannot be changed.");

        if (scorecard is null)
        {
            scorecard = new Scorecard
            {
                TenantId = interview.TenantId,
                InterviewId = interviewId,
                InterviewerUserId = userId,
                ScorecardTemplateId = interview.ScorecardTemplateId,
                Status = ScorecardStatus.Draft,
            };
            _db.Scorecards.Add(scorecard);
        }

        HireRecommendation? recommendation = null;
        if (!string.IsNullOrWhiteSpace(request.Recommendation))
        {
            if (!Enum.TryParse<HireRecommendation>(
                    request.Recommendation, ignoreCase: false, out var parsed))
                throw new InvalidOperationException(
                    $"'{request.Recommendation}' is not a recommendation.");
            recommendation = parsed;
        }

        scorecard.Recommendation = recommendation;
        scorecard.SummaryComment = request.SummaryComment;

        var criteria = await LoadCriteriaAsync(interview.ScorecardTemplateId, ct);
        var byId = criteria.ToDictionary(c => c.Id);

        // Answers are rebuilt from the template rather than stored as sent — the same
        // approach ApplicationFormSchema takes for public application answers. An answer
        // against a criterion that is not on this interview's template is dropped rather
        // than persisted, so a stale form cannot write arbitrary rows.
        var previous = await _db.ScorecardResponses
            .Where(r => r.ScorecardId == scorecard.Id)
            .ToListAsync(ct);
        _db.ScorecardResponses.RemoveRange(previous);

        var answered = new HashSet<Guid>();
        foreach (var answer in request.Answers)
        {
            if (!byId.TryGetValue(answer.ScorecardCriterionId, out var criterion)) continue;
            if (!answered.Add(criterion.Id)) continue;

            ValidateAnswer(criterion, answer);

            _db.ScorecardResponses.Add(new ScorecardResponse
            {
                TenantId = interview.TenantId,
                ScorecardId = scorecard.Id,
                ScorecardCriterionId = criterion.Id,
                // Snapshot (ADR-0017 §2): renaming a criterion later must not rewrite what
                // this person was actually asked.
                CriterionLabel = criterion.Label,
                CriterionType = criterion.Type,
                Rating = criterion.Type == CriterionType.Rating ? answer.Rating : null,
                YesNo = criterion.Type == CriterionType.YesNo ? answer.YesNo : null,
                Comment = answer.Comment,
            });
        }

        if (submit)
        {
            // Completeness is only enforced at submit. A half-finished draft is the normal
            // state of a scorecard during an interview.
            var missing = criteria
                .Where(c => c.IsRequired && !answered.Contains(c.Id))
                .Select(c => c.Label)
                .ToList();

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"Answer the required criteria before submitting: {string.Join(", ", missing)}.");

            if (recommendation is null)
                throw new InvalidOperationException(
                    "A submitted scorecard needs an overall recommendation.");

            scorecard.Status = ScorecardStatus.Submitted;
            scorecard.SubmittedAt = _clock.GetUtcNow();
        }

        await _db.SaveChangesAsync(ct);

        return await BuildMineAsync(interview, scorecard, ct);
    }

    public async Task<InterviewScorecardsDto?> GetForInterviewAsync(
        Guid interviewId, CancellationToken ct = default)
    {
        var reach = await _access.ResolveByInterviewAsync(interviewId, ct);
        if (reach is null) return null;

        var userId = _user.UserId;

        // The blind rule turns on PARTICIPATION, not on reach. A recruiter who is not on
        // the panel is not at risk of anchoring their own assessment — they aren't writing
        // one — and blinding them would lock them out of their own pipeline. A recruiter
        // who IS on the panel is blinded like anybody else.
        var isParticipant = await _access.IsParticipantAsync(interviewId, ct);

        var hasSubmittedOwn = userId is not null && await _db.Scorecards.AsNoTracking()
            .AnyAsync(s => s.InterviewId == interviewId
                           && s.InterviewerUserId == userId.Value
                           && s.Status == ScorecardStatus.Submitted, ct);

        var blinded = isParticipant && !hasSubmittedOwn;

        var all = await _db.Scorecards.AsNoTracking()
            .Where(s => s.InterviewId == interviewId)
            .ToListAsync(ct);

        // A draft is nobody's opinion yet — visible to its author only, always, including
        // to company-wide roles.
        var readable = all
            .Where(s => s.Status == ScorecardStatus.Submitted
                        || (userId is not null && s.InterviewerUserId == userId.Value))
            .ToList();

        var visible = blinded
            ? readable.Where(s => userId is not null && s.InterviewerUserId == userId.Value).ToList()
            : readable;

        var hiddenCount = readable.Count - visible.Count;

        var dtos = await MapScorecardsAsync(visible, ct);

        return new InterviewScorecardsDto(
            interviewId,
            dtos.OrderBy(d => d.InterviewerName).ToList(),
            hiddenCount,
            blinded);
    }

    // ---------- helpers ----------

    /// <summary>Loads an interview the caller may evaluate — i.e. one they are on the panel
    /// of. Reach alone is not enough: a recruiter can read this interview, but writing an
    /// evaluation for a conversation they were not in would be fabricating evidence.</summary>
    private async Task<Interview?> LoadForParticipantAsync(Guid interviewId, CancellationToken ct)
    {
        if (_user.UserId is null) return null;

        var reach = await _access.ResolveByInterviewAsync(interviewId, ct);
        if (reach is null) return null;

        if (!await _access.IsParticipantAsync(interviewId, ct)) return null;

        return await _db.Interviews.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == interviewId, ct);
    }

    private async Task<Scorecard?> LoadMyScorecardAsync(Guid interviewId, CancellationToken ct)
    {
        var userId = _user.UserId;
        if (userId is null) return null;

        return await _db.Scorecards.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.InterviewId == interviewId && s.InterviewerUserId == userId.Value, ct);
    }

    private async Task<List<ScorecardCriterion>> LoadCriteriaAsync(
        Guid? templateId, CancellationToken ct)
    {
        if (templateId is null) return new List<ScorecardCriterion>();

        return await _db.ScorecardCriteria.AsNoTracking()
            .Where(c => c.ScorecardTemplateId == templateId.Value)
            .OrderBy(c => c.Sequence)
            .ToListAsync(ct);
    }

    private static void ValidateAnswer(ScorecardCriterion criterion, ScorecardAnswerInput answer)
    {
        switch (criterion.Type)
        {
            case CriterionType.Rating:
                if (answer.Rating is null or < 1 or > 5)
                    throw new InvalidOperationException(
                        $"'{criterion.Label}' needs a rating from 1 to 5.");
                break;

            case CriterionType.YesNo:
                if (answer.YesNo is null)
                    throw new InvalidOperationException(
                        $"'{criterion.Label}' needs a yes or no.");
                break;

            case CriterionType.Text:
                if (string.IsNullOrWhiteSpace(answer.Comment))
                    throw new InvalidOperationException(
                        $"'{criterion.Label}' needs a written answer.");
                break;
        }
    }

    private async Task<MyScorecardDto> BuildMineAsync(
        Interview interview, Scorecard? scorecard, CancellationToken ct)
    {
        var criteria = await LoadCriteriaAsync(interview.ScorecardTemplateId, ct);

        string? templateName = null;
        if (interview.ScorecardTemplateId is not null)
        {
            templateName = await _db.ScorecardTemplates.AsNoTracking()
                .Where(t => t.Id == interview.ScorecardTemplateId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        var dto = scorecard is null
            ? null
            : (await MapScorecardsAsync(new[] { scorecard }, ct)).Single();

        return new MyScorecardDto(
            interview.Id,
            interview.ScorecardTemplateId,
            templateName,
            criteria
                .Select(c => new ScorecardCriterionDto(
                    c.Id, c.Sequence, c.Label, c.Guidance, c.Type.ToString(), c.IsRequired))
                .ToList(),
            dto);
    }

    private async Task<IReadOnlyList<ScorecardDto>> MapScorecardsAsync(
        IReadOnlyCollection<Scorecard> scorecards, CancellationToken ct)
    {
        if (scorecards.Count == 0) return Array.Empty<ScorecardDto>();

        var ids = scorecards.Select(s => s.Id).ToList();
        var userIds = scorecards.Select(s => s.InterviewerUserId).Distinct().ToList();

        var responses = await _db.ScorecardResponses.AsNoTracking()
            .Where(r => ids.Contains(r.ScorecardId))
            .ToListAsync(ct);

        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        // Projected in memory: EF Core 10 will not translate enum.ToString().
        return scorecards.Select(s => new ScorecardDto(
            s.Id,
            s.InterviewId,
            s.InterviewerUserId,
            names.GetValueOrDefault(s.InterviewerUserId) ?? "Unknown",
            s.Status.ToString(),
            s.SubmittedAt,
            s.Recommendation?.ToString(),
            s.SummaryComment,
            responses
                .Where(r => r.ScorecardId == s.Id)
                .Select(r => new ScorecardResponseDto(
                    r.ScorecardCriterionId,
                    r.CriterionLabel,
                    r.CriterionType.ToString(),
                    r.Rating,
                    r.YesNo,
                    r.Comment))
                .ToList()
        )).ToList();
    }
}
