using System.Security.Cryptography;
using System.Text;
using Gym.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Gym.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Random 256-bit tokens, stored only as SHA-256 hashes.</summary>
public sealed class SecureTokenService : ISecureTokenService
{
    public (string Token, string Hash) CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(bytes).ToLowerInvariant();
        return (token, Hash(token));
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

/// <summary>
/// HMAC of the client session id with a key that rotates daily. Buckets cannot be linked
/// across days and the raw session id is never stored.
/// </summary>
public sealed class SessionBucketHasher(IConfiguration configuration, IClock clock) : ISessionBucketHasher
{
    public string Hash(string sessionId)
    {
        var secret = configuration["Analytics:HashSecret"] ?? "local-dev-analytics-secret";
        var day = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var key = SHA256.HashData(Encoding.UTF8.GetBytes($"{secret}:{day}"));
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sessionId));
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }
}
