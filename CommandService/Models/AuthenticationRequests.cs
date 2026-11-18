using System.ComponentModel.DataAnnotations;

namespace CommandService.Models;

/// <summary>Credentials and profile details for creating an account.</summary>
public sealed record RegisterRequest
{
	[Required, StringLength(100, MinimumLength = 2)]
	public required string DisplayName { get; init; }

	[Required, EmailAddress, StringLength(320)]
	public required string Email { get; init; }

	[Required, MinLength(8), MaxLength(128)]
	public required string Password { get; init; }
}

/// <summary>Credentials for obtaining an access token.</summary>
public sealed record LoginRequest
{
	[Required, EmailAddress, StringLength(320)]
	public required string Email { get; init; }

	[Required, MinLength(8), MaxLength(128)]
	public required string Password { get; init; }
}
