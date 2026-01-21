using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Selfmx.Api.Data;
using Selfmx.Api.Entities;
using Selfmx.Api.Settings;

namespace Selfmx.Api.Services;

public class DomainService
{
    private readonly AppDbContext _db;
    private readonly AppSettings _settings;

    public DomainService(AppDbContext db, IOptions<AppSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<Domain?> GetByIdAsync(string id, CancellationToken ct = default) =>
        await _db.Domains.FindAsync([id], ct);

    public async Task<Domain?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await _db.Domains.FirstOrDefaultAsync(d => d.Name == name, ct);

    public async Task<(Domain[] Items, int Total)> ListAsync(int page, int limit, CancellationToken ct = default)
    {
        var total = await _db.Domains.CountAsync(ct);
        var items = await _db.Domains
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToArrayAsync(ct);

        return (items, total);
    }

    public async Task<Domain> CreateAsync(string name, CancellationToken ct = default)
    {
        var domain = new Domain
        {
            Id = Guid.NewGuid().ToString(),
            Name = name.ToLowerInvariant(),
            Status = DomainStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Domains.Add(domain);
        await _db.SaveChangesAsync(ct);

        return domain;
    }

    public async Task UpdateAsync(Domain domain, CancellationToken ct = default)
    {
        _db.Domains.Update(domain);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Domain domain, CancellationToken ct = default)
    {
        _db.Domains.Remove(domain);
        await _db.SaveChangesAsync(ct);
    }

    public bool IsTimedOut(Domain domain) =>
        domain.Status == DomainStatus.Verifying
        && domain.VerificationStartedAt.HasValue
        && DateTime.UtcNow - domain.VerificationStartedAt.Value > _settings.VerificationTimeout;

    public async Task<Domain[]> GetDomainsNeedingVerificationAsync(CancellationToken ct = default) =>
        await _db.Domains
            .Where(d => d.Status == DomainStatus.Verifying)
            .ToArrayAsync(ct);
}
