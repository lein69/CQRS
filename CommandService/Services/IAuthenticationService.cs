using CommandService.Models;

namespace CommandService.Services;

public interface IAuthenticationService
{
	Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
	Task<AuthenticationResponse> RegisterAsync(
		RegisterRequest request,
		CancellationToken cancellationToken);
	Task<AuthenticationResponse?> LoginAsync(
		LoginRequest request,
		CancellationToken cancellationToken);
	Task<UserProfileResponse?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken);
}
