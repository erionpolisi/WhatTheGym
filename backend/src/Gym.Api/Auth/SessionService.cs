using System.Security.Claims;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Gym.Api.Auth;

public static class ClaimsExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static UserRole GetRole(this ClaimsPrincipal principal) =>
        Enum.TryParse<UserRole>(principal.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.User;
}

/// <summary>
/// BFF session handling: cookie sign-in plus rotating, hashed, server-side refresh tokens.
/// Tokens are never exposed to the frontend beyond HttpOnly cookies.
/// </summary>
public sealed class SessionService(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    ISecureTokenService tokenService,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<AuthOptions> authOptions,
    IHostEnvironment environment)
{
    public const string RefreshCookieName = "wtg.refresh";
    private const string RefreshCookiePath = "/api/v1/auth";

    public static ClaimsPrincipal BuildPrincipal(MeDto me)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, me.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, me.DisplayName));
        identity.AddClaim(new Claim(ClaimTypes.Email, me.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, me.Role));
        identity.AddClaim(new Claim("email_verified", me.EmailVerified ? "true" : "false"));
        return new ClaimsPrincipal(identity);
    }

    public async Task SignInAsync(HttpContext httpContext, MeDto me)
    {
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(me),
            new AuthenticationProperties { IsPersistent = true });
        await IssueRefreshTokenAsync(httpContext, me.Id);
    }

    public async Task IssueRefreshTokenAsync(HttpContext httpContext, Guid userId)
    {
        var (token, hash) = tokenService.CreateToken();
        var lifetime = TimeSpan.FromDays(authOptions.Value.RefreshTokenLifetimeDays);
        refreshTokens.Add(RefreshToken.Create(userId, hash, clock.UtcNow, lifetime));
        await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);
        SetRefreshCookie(httpContext, token, lifetime);
    }

    public async Task<Result<MeDto>> RotateAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var token) || string.IsNullOrEmpty(token))
        {
            return Result.Failure<MeDto>(Error.Unauthorized("auth.noRefreshToken", "Kein Refresh-Token vorhanden."));
        }

        var stored = await refreshTokens.GetByHashAsync(tokenService.Hash(token), httpContext.RequestAborted);
        if (stored is null)
        {
            return Result.Failure<MeDto>(Error.Unauthorized("auth.invalidRefreshToken", "Ungueltiges Refresh-Token."));
        }

        if (!stored.IsActive(clock.UtcNow))
        {
            // Reuse of a rotated token: revoke the whole session family of this user.
            if (stored.ReplacedByTokenHash is not null)
            {
                await refreshTokens.RevokeAllForUserAsync(stored.UserId, clock.UtcNow, httpContext.RequestAborted);
                await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);
            }

            ClearRefreshCookie(httpContext);
            return Result.Failure<MeDto>(Error.Unauthorized("auth.refreshTokenExpired", "Das Refresh-Token ist abgelaufen oder widerrufen."));
        }

        var user = await users.GetByIdAsync(stored.UserId, httpContext.RequestAborted);
        if (user is null || user.Status != UserStatus.Active)
        {
            stored.Revoke(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);
            ClearRefreshCookie(httpContext);
            return Result.Failure<MeDto>(Error.Unauthorized("auth.userInactive", "Das Konto ist nicht aktiv."));
        }

        var (newToken, newHash) = tokenService.CreateToken();
        var lifetime = TimeSpan.FromDays(authOptions.Value.RefreshTokenLifetimeDays);
        stored.Revoke(clock.UtcNow, newHash);
        refreshTokens.Add(RefreshToken.Create(user.Id, newHash, clock.UtcNow, lifetime));
        await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);
        SetRefreshCookie(httpContext, newToken, lifetime);

        return new MeDto(user.Id, user.Email, user.EmailVerified, user.DisplayName, user.Role.ToString());
    }

    public async Task RevokeAsync(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var token) && !string.IsNullOrEmpty(token))
        {
            var stored = await refreshTokens.GetByHashAsync(tokenService.Hash(token), httpContext.RequestAborted);
            if (stored is not null)
            {
                stored.Revoke(clock.UtcNow);
                await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);
            }
        }

        ClearRefreshCookie(httpContext);
    }

    private void SetRefreshCookie(HttpContext httpContext, string token, TimeSpan lifetime) =>
        httpContext.Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Outside Development the Secure flag is mandatory regardless of what the
            // (possibly TLS-terminating) proxy reports for the inbound scheme.
            Secure = !environment.IsDevelopment() || httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            MaxAge = lifetime,
        });

    private static void ClearRefreshCookie(HttpContext httpContext) =>
        httpContext.Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
}
