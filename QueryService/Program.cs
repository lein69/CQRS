using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QueryService.Repositories;
using System.Text;

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

builder.Services.AddControllers();
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
BsonSerializer.RegisterSerializer(new GuidSerializer(
	GuidRepresentation.Standard));
var allowedOrigins = builder.Configuration
	.GetSection("Cors:AllowedOrigins")
	.Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy(
	"DemoFrontend",
	policy => policy.WithOrigins(allowedOrigins)
		.AllowAnyHeader()
		.AllowAnyMethod()));
//Add Service to DI Container
builder.Services.AddSingleton<IMongoClient>(sp =>
{
	var config = builder.Configuration.GetConnectionString("MongoDB");
	if (string.IsNullOrWhiteSpace(config))
	{
		throw new InvalidOperationException(
			"ConnectionStrings:MongoDB is not configured.");
	}

	return new MongoClient(config);
});
builder.Services.AddScoped<IStudyGroupRepository, StudyGroupRepository>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseCors("DemoFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
