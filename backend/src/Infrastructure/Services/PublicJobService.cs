using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>The anonymous applicant-facing surface (Module 2.1/2.2).
///
/// <para>⚠️ <b>There is no tenant claim on any request that reaches this class.</b> The
/// tenant query filters compare against <c>_tenant.TenantId</c>, which is <c>Guid.Empty</c>
/// here, so an ordinary query returns nothing — every read below therefore uses
/// <c>IgnoreQueryFilters()</c> and re-applies the tenant constraint by hand from the tenant
/// the <b>token</b> resolves to. That is the whole security model of this file: the token
/// establishes the tenant, and nothing is ever read or written outside it.</para>
///
/// <para>The same reasoning applies to writes: <c>AppDbContext</c> stamps
/// <c>TenantId</c> from the (empty) claim on new rows, so every entity created here sets
/// <c>TenantId</c> explicitly. Forgetting would save an orphan row that no tenant can
/// ever read back.</para>
/// </summary>
public class PublicJobService : IPublicJobService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public PublicJobService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PublicJobDto?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var found = await ResolveAsync(token, ct);
        if (found is null) return null;

        var (link, posting) = found.Value;

        var company = await _db.Companies.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == posting.TenantId, ct);

        // Counted on read rather than in a background job: the number is a nice-to-have,
        // and a wrong-but-cheap view count is better than a write path that can fail the
        // page load. Not deduplicated — this is a share metric, not analytics.
        link.ViewCount++;
        await _db.SaveChangesAsync(ct);

        return new PublicJobDto(
            posting.Title,
            posting.Description,
            posting.Location,
            posting.EmploymentType.ToString(),
            company?.Name ?? string.Empty,
            FormatSalary(posting),
            posting.ApplicationFormFieldsJson,
            IsOpen: posting.Status == JobStatus.Live);
    }

    public async Task<SubmitApplicationResponse?> SubmitAsync(
        string token, SubmitApplicationRequest request, CancellationToken ct = default)
    {
        var found = await ResolveAsync(token, ct);
        if (found is null) return null;

        var (link, posting) = found.Value;

        if (posting.Status != JobStatus.Live)
            throw new InvalidOperationException("This vacancy is no longer accepting applications.");

        // At least one contact method, or nobody can ever be told the outcome — and
        // duplicate detection has nothing to key on.
        var email = ContactNormalizer.Email(request.Email);
        var phone = ContactNormalizer.Phone(request.Phone);
        if (email is null && phone is null)
            throw new InvalidOperationException("Please provide an email address or a phone number.");

        // Custom-field answers are validated against the posting's own schema and REBUILT
        // from it — the applicant's JSON is never stored as sent. Without this, "custom
        // fields" would be an anonymous write of arbitrary JSON into the customer's database.
        if (!ApplicationFormSchema.TryValidateAnswers(
                posting.ApplicationFormFieldsJson, request.CustomFieldsJson,
                out var customFields, out var fieldError))
        {
            throw new InvalidOperationException(fieldError!);
        }

        var now = _clock.GetUtcNow();
        var tenantId = posting.TenantId;

        // ── Duplicate detection (Module 2.7) ──
        // Match on either contact field, within this tenant only. Reusing the candidate is
        // what makes the 360° history real: apply twice and it is one person with two
        // applications, not two strangers who happen to share a phone number.
        var candidate = await _db.Candidates.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId
                        && c.MergedIntoCandidateId == null
                        && ((email != null && c.Email == email) || (phone != null && c.Phone == phone)))
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (candidate is null)
        {
            candidate = new Candidate
            {
                TenantId = tenantId,
                FullName = request.FullName.Trim(),
                Email = email,
                Phone = phone,
                Source = SourceChannel.Direct,
            };
            _db.Candidates.Add(candidate);
        }
        else
        {
            // Fill blanks from this submission, but never overwrite: a returning applicant
            // giving a phone this time shouldn't erase the email we already had, and a
            // stranger sharing a household number must not rewrite someone else's name.
            candidate.Email ??= email;
            candidate.Phone ??= phone;
        }

        // Ids are assigned in the constructor (BaseEntity), so the three rows below can be
        // wired together and saved in one transaction without an intermediate round-trip.
        var application = new JobApplication
        {
            TenantId = tenantId,
            JobPostingId = posting.Id,
            CandidateId = candidate.Id,
            Status = PipelineStatus.Applied,
            Source = SourceChannel.Direct,
            AppliedAt = now,
            CoverNote = request.CoverNote,
            CustomFieldsJson = customFields,
        };
        _db.JobApplications.Add(application);

        // The first history row, written at the same moment as the application itself.
        // Module 5 measures time-in-stage from these; a row that isn't written when it
        // happens can never be reconstructed afterwards.
        _db.ApplicationStageHistories.Add(new ApplicationStageHistory
        {
            TenantId = tenantId,
            JobApplicationId = application.Id,
            FromStatus = null,
            ToStatus = PipelineStatus.Applied,
            ChangedAt = now,
            ChangedByUserId = null, // nobody is logged in
            Note = "Applied via public job page.",
        });

        link.ApplyCount++;

        await _db.SaveChangesAsync(ct);

        // Deliberately no ids and no "you have already applied" hint — see the DTO.
        return new SubmitApplicationResponse(
            "Thank you. Your application has been received.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Resolves a token to its link and posting, or null when the link should not
    /// be honoured. Every reason returns the same null so the caller can answer with one
    /// 404: distinguishing "revoked" from "expired" from "never existed" would tell someone
    /// probing tokens which guesses were close.</summary>
    private async Task<(PortalLink Link, JobPosting Posting)?> ResolveAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        // IgnoreQueryFilters: there is no tenant claim on an anonymous request, so the
        // filter would exclude every row. Token uniqueness is enforced by the schema.
        var link = await _db.PortalLinks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Token == token, ct);

        if (link is null || link.IsRevoked) return null;
        if (link.ExpiresAt is not null && link.ExpiresAt <= _clock.GetUtcNow()) return null;

        var posting = await _db.JobPostings.IgnoreQueryFilters()
            // Tenant re-applied by hand from the token's own row — the filter is off, so
            // this is what keeps a token from ever reaching another company's data.
            .FirstOrDefaultAsync(p => p.Id == link.JobPostingId && p.TenantId == link.TenantId, ct);

        // A Draft posting has no public existence, even if a token somehow exists.
        if (posting is null || posting.Status == JobStatus.Draft) return null;

        return (link, posting);
    }

    private static string? FormatSalary(JobPosting p)
    {
        if (!p.ShowSalary) return null;
        if (p.SalaryMin is null && p.SalaryMax is null) return null;
        if (p.SalaryMin is not null && p.SalaryMax is not null && p.SalaryMin != p.SalaryMax)
            return $"{p.SalaryMin:N0} – {p.SalaryMax:N0}";
        return $"{(p.SalaryMin ?? p.SalaryMax):N0}";
    }
}
