using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CommandService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace CommandService.Services;

public sealed class AuthenticationService : IAuthenticationService
{
	private const int PasswordIterations = 310_000;
	private const int PasswordSaltSize = 16;
	private const int PasswordHashSize = 32;
	private readonly string _connectionString;
	private readonly string _issuer;
	private readonly string _audience;
	private readonly byte[] _signingKey;
	private readonly int _tokenLifetimeMinutes;

	public AuthenticationService(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString("SqlServer")
			?? throw new InvalidOperationException(
				"ConnectionStrings:SqlServer is not configured.");
		_issuer = configuration["Authentication:Issuer"]
			?? throw new InvalidOperationException("Authentication:Issuer is not configured.");
		_audience = configuration["Authentication:Audience"]
			?? throw new InvalidOperationException("Authentication:Audience is not configured.");
		var signingKey = configuration["Authentication:SigningKey"]
			?? throw new InvalidOperationException(
				"Authentication:SigningKey is not configured.");
		_signingKey = Encoding.UTF8.GetBytes(signingKey);
		if (_signingKey.Length < 32)
		{
			throw new InvalidOperationException(
				"Authentication:SigningKey must contain at least 32 bytes.");
		}

		_tokenLifetimeMinutes = configuration.GetValue<int?>(
			"Authentication:TokenLifetimeMinutes") ?? 60;
		if (_tokenLifetimeMinutes is < 5 or > 1440)
		{
			throw new InvalidOperationException(
				"Authentication:TokenLifetimeMinutes must be between 5 and 1440.");
		}
	}

	public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
	{
		const string sql = """
			IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
			BEGIN
				CREATE TABLE dbo.Accounts (
					Id uniqueidentifier NOT NULL PRIMARY KEY,
					DisplayName nvarchar(100) NOT NULL,
					Email nvarchar(320) NOT NULL,
					EmailNormalized nvarchar(320) NOT NULL,
					PasswordHash nvarchar(256) NOT NULL,
					CreatedAtUtc datetime2 NOT NULL
				);
				CREATE UNIQUE INDEX UX_Accounts_EmailNormalized
					ON dbo.Accounts (EmailNormalized);
			END;
			""";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = new SqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<AuthenticationResponse> RegisterAsync(
		RegisterRequest request,
		CancellationToken cancellationToken)
	{
		var account = new UserProfileResponse(
			Guid.NewGuid(),
			request.DisplayName.Trim(),
			request.Email.Trim(),
			DateTimeOffset.UtcNow);
		var emailNormalized = NormalizeEmail(account.Email);
		var passwordHash = HashPassword(request.Password);
		const string sql = """
			INSERT INTO dbo.Accounts
				(Id, DisplayName, Email, EmailNormalized, PasswordHash, CreatedAtUtc)
			VALUES
				(@Id, @DisplayName, @Email, @EmailNormalized, @PasswordHash, @CreatedAtUtc);
			""";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@Id", account.Id);
		command.Parameters.AddWithValue("@DisplayName", account.DisplayName);
		command.Parameters.AddWithValue("@Email", account.Email);
		command.Parameters.AddWithValue("@EmailNormalized", emailNormalized);
		command.Parameters.AddWithValue("@PasswordHash", passwordHash);
		command.Parameters.AddWithValue("@CreatedAtUtc", account.CreatedAtUtc.UtcDateTime);
		try
		{
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqlException exception) when (exception.Number is 2601 or 2627)
		{
			throw new EmailAlreadyRegisteredException();
		}

		return CreateAuthenticationResponse(account);
	}

	public async Task<AuthenticationResponse?> LoginAsync(
		LoginRequest request,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT Id, DisplayName, Email, PasswordHash, CreatedAtUtc
			FROM dbo.Accounts
			WHERE EmailNormalized = @EmailNormalized;
			""";
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@EmailNormalized", NormalizeEmail(request.Email));
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		var passwordHash = reader.GetString(3);
		if (!VerifyPassword(request.Password, passwordHash))
		{
			return null;
		}

		var account = new UserProfileResponse(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetString(2),
			new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero));
		return CreateAuthenticationResponse(account);
	}

	public async Task<UserProfileResponse?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT Id, DisplayName, Email, CreatedAtUtc
			FROM dbo.Accounts
			WHERE Id = @Id;
			""";
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@Id", id);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new UserProfileResponse(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetString(2),
			new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero));
	}

	private AuthenticationResponse CreateAuthenticationResponse(UserProfileResponse account)
	{
		var now = DateTime.UtcNow;
		var expiresAt = now.AddMinutes(_tokenLifetimeMinutes);
		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
			new Claim(JwtRegisteredClaimNames.Email, account.Email),
			new Claim(JwtRegisteredClaimNames.Name, account.DisplayName)
		};
		var token = new JwtSecurityToken(
			_issuer,
			_audience,
			claims,
			now,
			expiresAt,
			new SigningCredentials(
				new SymmetricSecurityKey(_signingKey),
				SecurityAlgorithms.HmacSha256));

		return new AuthenticationResponse(
			new JwtSecurityTokenHandler().WriteToken(token),
			"Bearer",
			new DateTimeOffset(expiresAt, TimeSpan.Zero),
			account);
	}

	private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

	private static string HashPassword(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
		var hash = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			PasswordIterations,
			HashAlgorithmName.SHA256,
			PasswordHashSize);
		return string.Join(
			'$',
			"v1",
			PasswordIterations.ToString(CultureInfo.InvariantCulture),
			Convert.ToBase64String(salt),
			Convert.ToBase64String(hash));
	}

	private static bool VerifyPassword(string password, string encodedHash)
	{
		var parts = encodedHash.Split('$');
		if (parts.Length != 4 ||
			parts[0] != "v1" ||
			!int.TryParse(
				parts[1],
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var iterations) ||
			iterations is < 100_000 or > 1_000_000)
		{
			throw new InvalidDataException("Stored password hash has an unsupported format.");
		}

		var salt = Convert.FromBase64String(parts[2]);
		var expectedHash = Convert.FromBase64String(parts[3]);
		if (salt.Length != PasswordSaltSize || expectedHash.Length != PasswordHashSize)
		{
			throw new InvalidDataException("Stored password hash has invalid lengths.");
		}

		var actualHash = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			iterations,
			HashAlgorithmName.SHA256,
			expectedHash.Length);
		return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
	}
}

public sealed class EmailAlreadyRegisteredException : Exception
{
	public EmailAlreadyRegisteredException()
		: base("An account with this email already exists.")
	{
	}
}
