using CommandService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var jwtSigningKey = builder.Configuration["Authentication:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
	throw new InvalidOperationException(
		"Authentication:SigningKey must be configured with at least 32 bytes.");
}

var jwtIssuer = builder.Configuration["Authentication:Issuer"]
	?? throw new InvalidOperationException("Authentication:Issuer is not configured.");
var jwtAudience = builder.Configuration["Authentication:Audience"]
	?? throw new InvalidOperationException("Authentication:Audience is not configured.");

// Add services to the container.
builder.Services.AddControllers()
	.AddJsonOptions(options =>
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwtIssuer,
			ValidateAudience = true,
			ValidAudience = jwtAudience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromSeconds(30)
		};
	});
builder.Services.AddAuthorization();

//Add service to DI Container
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IStudyGroupService, StudyGroupService>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
	var authenticationService = scope.ServiceProvider
		.GetRequiredService<IAuthenticationService>();
	await authenticationService.InitializeDatabaseAsync();
	var studyGroupService = scope.ServiceProvider.GetRequiredService<IStudyGroupService>();
	await studyGroupService.InitializeDatabaseAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
