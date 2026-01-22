using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Services;

public class ApiKeyService
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(AppDbContext db, IServiceScopeFactory scopeFactory, ILogger<ApiKeyService> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<(ApiKey Key, string PlainTextKey)> CreateAsync(
        string name,
        string[] domainIds,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        // Generate key: re_ + 32 random chars (base62)
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        if (randomPart.Length > 32)
            randomPart = randomPart[..32];

        var prefix = isAdmin ? "re_admin_" : "re_";
        var plainTextKey = $"{prefix}{randomPart}";
        var keyPrefix = plainTextKey[..11]; // "re_xxxxxxxx" or "re_admin_xx"

        // Use SHA256 + salt (NOT BCrypt - too slow for per-request validation)
        var salt = RandomNumberGenerator.GetBytes(16);
        var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextKey + Convert.ToBase64String(salt)));

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            KeyHash = Convert.ToBase64String(keyHash),
            KeySalt = Convert.ToBase64String(salt),
            KeyPrefix = keyPrefix,
            IsAdmin = isAdmin
        };

        // Add domain scopes (only for non-admin keys)
        if (!isAdmin)
        {
            foreach (var domainId in domainIds)
            {
                apiKey.AllowedDomains.Add(new ApiKeyDomain
                {
                    ApiKeyId = apiKey.Id,
                    DomainId = domainId
                });
            }
        }

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return (apiKey, plainTextKey);
    }

    public async Task<ApiKey?> ValidateAsync(string plainTextKey, string? ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(plainTextKey) || !plainTextKey.StartsWith("re_") || plainTextKey.Length < 11)
            return null;

        var prefix = plainTextKey[..11];

        // Find by prefix first (indexed), then verify hash
        var candidate = await _db.ApiKeys
            .Include(k => k.AllowedDomains)
            .Where(k => k.KeyPrefix == prefix && k.RevokedAt == null)
            .FirstOrDefaultAsync(ct);

        // CRITICAL: Always perform hash comparison to prevent timing attacks
        // Even if no candidate found, compare against dummy hash
        var saltBytes = candidate?.KeySalt is not null
            ? Convert.FromBase64String(candidate.KeySalt)
            : new byte[16]; // Dummy salt
        var expectedHash = candidate?.KeyHash is not null
            ? Convert.FromBase64String(candidate.KeyHash)
            : new byte[32]; // Dummy hash

        var computedHash = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextKey + Convert.ToBase64String(saltBytes)));

        // Constant-time comparison - CRITICAL for timing attack prevention
        if (!CryptographicOperations.FixedTimeEquals(computedHash, expectedHash) || candidate is null)
        {
            return null;
        }

        // Update last used - use separate scope to avoid change tracker issues
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await scopedDb.ApiKeys
                    .Where(k => k.Id == candidate.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(k => k.LastUsedAt, DateTime.UtcNow)
                        .SetProperty(k => k.LastUsedIp, ipAddress));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update LastUsedAt for key {Prefix}", candidate.KeyPrefix);
            }
        });

        return candidate;
    }

    public async Task<(ApiKey[] Items, int Total)> ListAsync(int page, int limit, CancellationToken ct = default)
    {
        var total = await _db.ApiKeys.CountAsync(ct);
        var items = await _db.ApiKeys
            .Include(k => k.AllowedDomains)
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToArrayAsync(ct);

        return (items, total);
    }

    public async Task<ApiKey?> GetByIdAsync(string id, CancellationToken ct = default) =>
        await _db.ApiKeys
            .Include(k => k.AllowedDomains)
            .FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task RevokeAsync(string id, CancellationToken ct = default)
    {
        await _db.ApiKeys
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, DateTime.UtcNow), ct);
    }

    public bool CanAccessDomain(ClaimsPrincipal user, string domainId)
    {
        // Admin can access all
        if (user.FindFirst("ActorType")?.Value == "admin")
            return true;

        // Check if domain is in allowed list
        return user.FindAll("AllowedDomain").Any(c => c.Value == domainId);
    }
}
