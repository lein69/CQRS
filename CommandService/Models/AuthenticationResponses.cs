namespace CommandService.Models;

/// <summary>Public account details returned by authentication endpoints.</summary>
public sealed record UserProfileResponse(
	Guid Id,
	string DisplayName,
	string Email,
	DateTimeOffset CreatedAtUtc);

/// <summary>Bearer access token and profile returned after registration or login.</summary>
public sealed record AuthenticationResponse(
	string AccessToken,
	string TokenType,
	DateTimeOffset ExpiresAtUtc,
	UserProfileResponse User);
