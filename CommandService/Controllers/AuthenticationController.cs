using System.Security.Claims;
using CommandService.Models;
using CommandService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommandService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController : ControllerBase
{
	private readonly IAuthenticationService _authenticationService;

	public AuthenticationController(IAuthenticationService authenticationService)
	{
		_authenticationService = authenticationService;
	}

	[HttpPost("register")]
	[ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<ActionResult<AuthenticationResponse>> Register(
		RegisterRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var response = await _authenticationService.RegisterAsync(
				request,
				cancellationToken);
			return CreatedAtAction(nameof(Me), response);
		}
		catch (EmailAlreadyRegisteredException)
		{
			return Conflict(new ProblemDetails
			{
				Status = StatusCodes.Status409Conflict,
				Title = "Email already registered",
				Detail = "An account with this email already exists."
			});
		}
	}

	[HttpPost("login")]
	[ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<AuthenticationResponse>> Login(
		LoginRequest request,
		CancellationToken cancellationToken)
	{
		var response = await _authenticationService.LoginAsync(request, cancellationToken);
		if (response is null)
		{
			return Unauthorized(new ProblemDetails
			{
				Status = StatusCodes.Status401Unauthorized,
				Title = "Invalid credentials",
				Detail = "Email or password is incorrect."
			});
		}

		return Ok(response);
	}

	[Authorize]
	[HttpGet("me")]
	[ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<UserProfileResponse>> Me(
		CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
		{
			return Unauthorized();
		}

		var user = await _authenticationService.GetByIdAsync(userId, cancellationToken);
		return user is null ? Unauthorized() : Ok(user);
	}
}
