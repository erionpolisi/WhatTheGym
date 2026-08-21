using Gym.Api.Auth;
using Gym.Api.Middleware;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IOptions<AuthOptions> authOptions,
    IOptions<CorsOptions> corsOptions,
    IOptions<MailOptions> mailOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Starts the Google login (Authorization Code Flow with PKCE) and redirects back afterwards.</summary>
    [HttpGet("google/start")]
    [EnableRateLimiting("auth")]
    public ActionResult StartGoogleLogin([FromQuery] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(authOptions.Value.GoogleClientId))
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Google-Login nicht konfiguriert",
                detail: "Google OAuth ist in dieser Umgebung nicht konfiguriert. Lokal steht der Dev-Login zur Verfuegung.");
        }

        return Challenge(
            new AuthenticationProperties { RedirectUri = ValidateReturnUrl(returnUrl) },
            "Google");
    }

    [HttpGet("login-failed")]
    public ActionResult LoginFailed() =>
        Problem(statusCode: StatusCodes.Status403Forbidden, title: "Anmeldung fehlgeschlagen",
            detail: "Die Anmeldung konnte nicht abgeschlossen werden.");

    /// <summary>Rotates the refresh token and renews the session cookie.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<MeDto>> Refresh([FromServices] SessionService session)
    {
        var result = await session.RotateAsync(HttpContext);
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            SessionService.BuildPrincipal(result.Value),
            new AuthenticationProperties { IsPersistent = true });
        return Ok(result.Value);
    }

    /// <summary>Logs out: revokes the refresh token and clears the session cookie.</summary>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout([FromServices] SessionService session)
    {
        await session.RevokeAsync(HttpContext);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    public sealed record DevLoginRequest(string Email, string DisplayName, bool EmailVerified = true);

    /// <summary>
    /// Development-only login without Google credentials. Requires Development environment
    /// AND Auth:EnableDevLogin=true. Never available in staging or production.
    /// </summary>
    [HttpPost("dev-login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<MeDto>> DevLogin(
        [FromServices] ICommandHandler<UpsertGoogleUserCommand, MeDto> upsertHandler,
        [FromServices] SessionService session,
        [FromBody] DevLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !authOptions.Value.EnableDevLogin)
        {
            return NotFound();
        }

        var result = await upsertHandler.Handle(
            new UpsertGoogleUserCommand($"dev:{request.Email.ToLowerInvariant()}", request.Email, request.EmailVerified, request.DisplayName),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        await session.SignInAsync(HttpContext, result.Value);
        return Ok(result.Value);
    }

    private string ValidateReturnUrl(string? returnUrl)
    {
        var fallback = mailOptions.Value.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return fallback;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute))
        {
            var origin = $"{absolute.Scheme}://{absolute.Authority}";
            return corsOptions.Value.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                ? returnUrl
                : fallback;
        }

        return returnUrl.StartsWith('/') ? $"{fallback.TrimEnd('/')}{returnUrl}" : fallback;
    }
}

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeDto>> Get(
        [FromServices] IQueryHandler<GetMeQuery, MeDto> handler,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new GetMeQuery(userId.Value), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record UpdateProfileRequest(string DisplayName);

    [HttpPut]
    public async Task<ActionResult<MeDto>> UpdateProfile(
        [FromServices] ICommandHandler<UpdateMyProfileCommand, MeDto> handler,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new UpdateMyProfileCommand(userId.Value, request.DisplayName), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>GDPR data export: everything stored about the calling account as JSON.</summary>
    [HttpGet("export")]
    public async Task<ActionResult<PersonalDataExportDto>> Export(
        [FromServices] IQueryHandler<ExportMyDataQuery, PersonalDataExportDto> handler,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new ExportMyDataQuery(userId.Value), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>GDPR account deletion: anonymizes the account and soft deletes own reviews (holds respected).</summary>
    [HttpDelete]
    public async Task<ActionResult> DeleteAccount(
        [FromServices] ICommandHandler<DeleteMyAccountCommand> handler,
        [FromServices] SessionService session,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new DeleteMyAccountCommand(userId.Value), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        await session.RevokeAsync(HttpContext);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("reviews")]
    public async Task<ActionResult<IReadOnlyList<OwnReviewDto>>> MyReviews(
        [FromServices] IQueryHandler<Gym.Application.Features.Reviews.ListMyReviewsQuery, IReadOnlyList<OwnReviewDto>> handler,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new Gym.Application.Features.Reviews.ListMyReviewsQuery(userId.Value), cancellationToken);
        return result.ToActionResult(this);
    }
}
