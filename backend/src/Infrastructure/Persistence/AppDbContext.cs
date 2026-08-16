using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Domain.Common;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenant _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant)
        : base(options) => _tenant = tenant;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobChannelPost> JobChannelPosts => Set<JobChannelPost>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<PortalLink> PortalLinks => Set<PortalLink>();
    public DbSet<ApplicationStageHistory> ApplicationStageHistories => Set<ApplicationStageHistory>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Module 1 — Requisition & Approval
    public DbSet<JdTemplate> JdTemplates => Set<JdTemplate>();
    public DbSet<ApprovalChain> ApprovalChains => Set<ApprovalChain>();
    public DbSet<ApprovalChainStep> ApprovalChainSteps => Set<ApprovalChainStep>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<RequisitionApproval> RequisitionApprovals => Set<RequisitionApproval>();

    // Module 3 — Interview & Assessment
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewParticipant> InterviewParticipants => Set<InterviewParticipant>();
    public DbSet<ScorecardTemplate> ScorecardTemplates => Set<ScorecardTemplate>();
    public DbSet<ScorecardCriterion> ScorecardCriteria => Set<ScorecardCriterion>();
    public DbSet<Scorecard> Scorecards => Set<Scorecard>();
    public DbSet<ScorecardResponse> ScorecardResponses => Set<ScorecardResponse>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteMention> NoteMentions => Set<NoteMention>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantAndTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTenantAndTimestamps();
        return base.SaveChanges();
    }

    /// <summary>Stamps <see cref="ITenantScoped.TenantId"/> on newly added rows.
    /// <para>Without this, a service that forgets to set TenantId saves the row with
    /// Guid.Empty — it then becomes invisible to the tenant query filter, so the write
    /// "succeeds" and the row can never be read back. That is a silent data-loss bug,
    /// and it is not a mistake any single service should have to remember not to make.</para>
    /// <para>Only fills when empty, so seeding and any deliberate assignment win.</para></summary>
    private void StampTenantAndTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added
                && entry.Entity is ITenantScoped scoped
                && scoped.TenantId == Guid.Empty)
            {
                scoped.TenantId = _tenant.TenantId;
            }

            if (entry.State == EntityState.Modified && entry.Entity is BaseEntity modified)
            {
                modified.UpdatedAt = now;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // NOTE: as the model grows past the MVP, split this into
        // IEntityTypeConfiguration<T> classes under Persistence/Configurations.

        // ---------- Company (one row per deployment, ADR-0004) ----------
        builder.Entity<Company>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(63); // DNS label limit
            e.HasIndex(x => x.Slug).IsUnique();                     // subdomain routing
            e.Property(x => x.LogoUrl).HasMaxLength(500);
        });

        // ---------- Department ----------
        builder.Entity<Department>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Code).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        // ---------- User ----------
        builder.Entity<User>(e =>
        {
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);

            // Globally unique, not per-tenant: login matches on email alone
            // (ADR-0002 known limitation). Enforcing it here makes that explicit
            // rather than leaving it as a latent ambiguity.
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.CustomRole)
             .WithMany(r => r.Users)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Dynamic RBAC (Requirement R2) ----------
        builder.Entity<Role>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Code).IsRequired().HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(1000);

            // TenantId + Code unique index (system roles have null TenantId)
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        builder.Entity<Permission>(e =>
        {
            e.Property(x => x.Module).IsRequired().HasMaxLength(100);
            e.Property(x => x.Feature).IsRequired().HasMaxLength(100);
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Code).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);

            // Permission Code unique index
            e.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });

            e.HasOne(x => x.Role)
             .WithMany(r => r.RolePermissions)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Permission)
             .WithMany(p => p.RolePermissions)
             .HasForeignKey(x => x.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- UserDepartment (access set — ADR-0003) ----------
        builder.Entity<UserDepartment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.DepartmentId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- JobPosting ----------
        builder.Entity<JobPosting>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).IsRequired();
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.SalaryMin).HasPrecision(18, 2);
            e.Property(x => x.SalaryMax).HasPrecision(18, 2);
            e.Property(x => x.ApplicationFormFieldsJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.TenantId, x.DepartmentId });
            e.HasIndex(x => new { x.TenantId, x.Status });

            // One posting per requisition — the guarantee is "every advertised role was
            // approved", and it is worth enforcing in the schema rather than trusting
            // every future code path to check.
            e.HasIndex(x => x.RequisitionId).IsUnique();

            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Requisition>().WithMany().HasForeignKey(x => x.RequisitionId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- JobChannelPost (Module 2/8 channel tracking) ----------
        builder.Entity<JobChannelPost>(e =>
        {
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.JobPostingId, x.Channel });
            e.HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobPostingId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Candidate ----------
        builder.Entity<Candidate>(e =>
        {
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(30);

            // Indexed for duplicate detection (2.7), deliberately NOT unique — two people
            // can share a household phone, and one person may legitimately appear twice.
            // Dedup is a detect-and-merge flow; a constraint here would reject real applicants.
            e.HasIndex(x => new { x.TenantId, x.Email });
            e.HasIndex(x => new { x.TenantId, x.Phone });
        });

        // ---------- JobApplication ----------
        builder.Entity<JobApplication>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.CoverNote).HasMaxLength(4000);
            e.Property(x => x.CustomFieldsJson).HasColumnType("jsonb");
            // Not unique: re-application to the same posting is a legitimate flow.
            e.HasIndex(x => new { x.JobPostingId, x.CandidateId });
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobPostingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Candidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- ApplicationStageHistory (append-only — Module 5 depends on it) ----------
        builder.Entity<ApplicationStageHistory>(e =>
        {
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => new { x.JobApplicationId, x.ChangedAt });
            e.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- PortalLink (public applicant job page) ----------
        builder.Entity<PortalLink>(e =>
        {
            e.Property(x => x.Token).IsRequired().HasMaxLength(64);
            // Unique globally, not per tenant: an anonymous request arrives with no tenant
            // context at all, so the token is the only thing that can resolve one.
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.JobPostingId);
            e.HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobPostingId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Module 1: Requisition & Approval ----------
        builder.Entity<JdTemplate>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Content).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.DepartmentId });
        });

        builder.Entity<ApprovalChain>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            // One active chain per department; DepartmentId null = company-wide default.
            e.HasIndex(x => new { x.TenantId, x.DepartmentId });
        });

        builder.Entity<ApprovalChainStep>(e =>
        {
            e.Property(x => x.Label).IsRequired().HasMaxLength(100);
            e.HasIndex(x => new { x.ApprovalChainId, x.Sequence }).IsUnique();
            e.HasOne<ApprovalChain>().WithMany().HasForeignKey(x => x.ApprovalChainId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Requisition>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.JobDescription).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SalaryBudget).HasPrecision(18, 2);
            // Department is the scoping axis (ADR-0003) — index it for the filtered list.
            e.HasIndex(x => new { x.TenantId, x.DepartmentId, x.Status });
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RequisitionApproval>(e =>
        {
            e.Property(x => x.Label).IsRequired().HasMaxLength(100);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            // HasDefaultValue backfills existing rows when the column is added. ValueGeneratedNever
            // is the important half: without it EF infers ValueGeneratedOnAdd from the default and
            // omits Round from the INSERT whenever the in-memory value is 0, letting Postgres
            // substitute 1. A future code path that forgot to set Round would then not throw — it
            // would silently file the step under round 1, which is a wrong answer rather than a
            // loud one. Always send the value we actually mean.
            e.Property(x => x.Round).HasDefaultValue(1).ValueGeneratedNever();
            // Round is part of the key, not decoration: a resubmission reuses sequences 1..n
            // (ADR-0023), so without it the second round violates this constraint on real
            // Postgres — and would NOT fail the suite, which runs on the in-memory provider.
            e.HasIndex(x => new { x.RequisitionId, x.Round, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.ApproverUserId, x.Decision }); // "awaiting my approval"
            e.HasOne<Requisition>().WithMany().HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Module 3: Interview & Assessment ----------
        builder.Entity<Interview>(e =>
        {
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Location).HasMaxLength(500);
            e.Property(x => x.Agenda).HasMaxLength(4000);
            e.Property(x => x.CancellationReason).HasMaxLength(1000);

            // Round is derived from the count of existing rounds, so it cannot collide —
            // but a unique index says so in the schema rather than trusting one method.
            e.HasIndex(x => new { x.JobApplicationId, x.Round }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ScheduledStart }); // "this week's interviews"

            e.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: deleting a template must not take the interviews scored
            // against it with it. Templates are deactivated, never deleted, for this reason.
            e.HasOne<ScorecardTemplate>().WithMany().HasForeignKey(x => x.ScorecardTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InterviewParticipant is an ACCESS GRANT (ADR-0017 §4), not just a join row.
        builder.Entity<InterviewParticipant>(e =>
        {
            e.HasIndex(x => new { x.InterviewId, x.UserId }).IsUnique();
            // Read hot path: "can this user reach this application" joins from here.
            e.HasIndex(x => x.UserId);

            e.HasOne<Interview>().WithMany().HasForeignKey(x => x.InterviewId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ScorecardTemplate>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.DepartmentId });
            e.HasIndex(x => new { x.TenantId, x.JobPostingId });

            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ScorecardCriterion>(e =>
        {
            e.Property(x => x.Label).IsRequired().HasMaxLength(200);
            e.Property(x => x.Guidance).HasMaxLength(1000);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.ScorecardTemplateId, x.Sequence }).IsUnique();

            e.HasOne<ScorecardTemplate>().WithMany().HasForeignKey(x => x.ScorecardTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Scorecard>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Recommendation).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SummaryComment).HasMaxLength(4000);

            // One evaluation per interviewer per round. Without this a retried submit could
            // produce two scorecards and the blind rule would show someone their own draft
            // as though it were a colleague's.
            e.HasIndex(x => new { x.InterviewId, x.InterviewerUserId }).IsUnique();

            e.HasOne<Interview>().WithMany().HasForeignKey(x => x.InterviewId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.InterviewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ScorecardTemplate>().WithMany().HasForeignKey(x => x.ScorecardTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ScorecardResponse>(e =>
        {
            // The snapshot columns (ADR-0017 §2) — required, because a response whose
            // criterion label is missing is unreadable once the template moves on.
            e.Property(x => x.CriterionLabel).IsRequired().HasMaxLength(200);
            e.Property(x => x.CriterionType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Comment).HasMaxLength(4000);
            e.HasIndex(x => new { x.ScorecardId, x.ScorecardCriterionId }).IsUnique();

            e.HasOne<Scorecard>().WithMany().HasForeignKey(x => x.ScorecardId)
                .OnDelete(DeleteBehavior.Cascade);

            // NO FK to ScorecardCriterion, on purpose. Editing a template replaces its
            // criteria rows; a constraint here would either block the edit or cascade the
            // delete into submitted evaluations. The snapshot columns are what make the
            // response readable, and the id is kept only to group analytics over the
            // current template.
        });

        builder.Entity<Note>(e =>
        {
            e.Property(x => x.Body).IsRequired().HasMaxLength(4000);
            e.HasIndex(x => new { x.JobApplicationId, x.CreatedAt });

            e.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Interview>().WithMany().HasForeignKey(x => x.InterviewId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NoteMention>(e =>
        {
            e.HasIndex(x => new { x.NoteId, x.MentionedUserId }).IsUnique();
            // "notes that mention me" — the inbox this will grow into.
            e.HasIndex(x => x.MentionedUserId);

            e.HasOne<Note>().WithMany().HasForeignKey(x => x.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.MentionedUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- RefreshToken ----------
        builder.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.Token).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.UserId });

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Tenant query filters ----------
        // Each company has its own database (ADR-0004), so these are a dormant safety
        // net against misconfiguration — NOT the primary isolation boundary. The
        // security-critical filter is department scoping (ADR-0003), applied explicitly
        // in the application layer.
        builder.Entity<Department>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<UserDepartment>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<JobPosting>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<JobChannelPost>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Candidate>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<JobApplication>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<ApplicationStageHistory>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        // ⚠️ PortalLink is filtered like everything else, but the public job endpoints MUST
        // read it with IgnoreQueryFilters(): an anonymous request carries no tenant_id claim,
        // so _tenant.TenantId is Guid.Empty and every lookup would return nothing. The token
        // is what establishes the tenant there — see PublicJobService.
        builder.Entity<PortalLink>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<JdTemplate>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<ApprovalChain>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<ApprovalChainStep>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Requisition>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<RequisitionApproval>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Interview>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<InterviewParticipant>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<ScorecardTemplate>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<ScorecardCriterion>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Scorecard>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<ScorecardResponse>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Note>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<NoteMention>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Role>().HasQueryFilter(e => e.TenantId == null || e.TenantId == _tenant.TenantId);
        builder.Entity<RefreshToken>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);

        base.OnModelCreating(builder);
    }
}
