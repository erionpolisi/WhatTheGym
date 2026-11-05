using Gym.Api.Auth;
using Gym.Application.Abstractions;
using Gym.Application.Contracts;
using Gym.Application.Features.Users;
using Microsoft.AspNetCore.Mvc;

namespace Gym.IntegrationTests;

[ApiController]
[Route("api/v1/test-auth")]
public sealed class TestAuthController : ControllerBase
{
    public sealed record LoginRequest(string Email, string DisplayName, bool EmailVerified = true);

    [HttpPost("login")]
    public async Task<ActionResult<MeDto>> Login(
        [FromServices] ICommandHandler<UpsertGoogleUserCommand, MeDto> upsertHandler,
        [FromServices] SessionService session,
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await upsertHandler.Handle(
            new UpsertGoogleUserCommand(
                $"test:{request.Email.ToLowerInvariant()}",
                request.Email,
                request.EmailVerified,
                request.DisplayName),
            cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest();
        }

        await session.SignInAsync(HttpContext, result.Value);
        return Ok(result.Value);
    }
}