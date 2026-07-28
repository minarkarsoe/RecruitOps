using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class ApprovalChainService : IApprovalChainService
{
    private readonly AppDbContext _db;

    public ApprovalChainService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApprovalChainDto>> GetChainsAsync(CancellationToken ct = default)
    {
        var chains = await _db.ApprovalChains.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var ids = chains.Select(c => c.Id).ToList();
        var steps = await _db.ApprovalChainSteps.AsNoTracking()
            .Where(s => ids.Contains(s.ApprovalChainId))
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

        return chains.Select(c => Map(c, steps)).ToList();
    }

    public async Task<ApprovalChainDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var chain = await _db.ApprovalChains.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (chain is null) return null;

        var steps = await _db.ApprovalChainSteps.AsNoTracking()
            .Where(s => s.ApprovalChainId == id)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

        return Map(chain, steps);
    }

    public async Task<ApprovalChainDto?> CreateAsync(CreateApprovalChainRequest request, CancellationToken ct = default)
    {
        if (request.DepartmentId is Guid deptId
            && !await _db.Departments.AnyAsync(d => d.Id == deptId, ct))
        {
            return null;
        }

        // Every approver must be a real user in this company — otherwise a requisition
        // could be submitted into a chain that nobody can ever action.
        var approverIds = request.Steps.Select(s => s.ApproverUserId).Distinct().ToList();
        var knownApprovers = await _db.Users.CountAsync(u => approverIds.Contains(u.Id), ct);
        if (knownApprovers != approverIds.Count) return null;

        var chain = new ApprovalChain
        {
            Name = request.Name,
            DepartmentId = request.DepartmentId,
            IsActive = true,
        };
        _db.ApprovalChains.Add(chain);

        // Sequence is derived from list order, so gaps and duplicates are impossible.
        var sequence = 1;
        foreach (var step in request.Steps)
        {
            _db.ApprovalChainSteps.Add(new ApprovalChainStep
            {
                ApprovalChainId = chain.Id,
                Sequence = sequence++,
                ApproverUserId = step.ApproverUserId,
                Label = step.Label,
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(chain.Id, ct);
    }

    private static ApprovalChainDto Map(ApprovalChain chain, List<ApprovalChainStep> allSteps) =>
        new(chain.Id,
            chain.Name,
            chain.DepartmentId,
            chain.IsActive,
            allSteps.Where(s => s.ApprovalChainId == chain.Id)
                    .OrderBy(s => s.Sequence)
                    .Select(s => new ApprovalChainStepDto(s.Sequence, s.ApproverUserId, s.Label))
                    .ToList());
}
