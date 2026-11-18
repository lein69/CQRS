using ConsumerService;
using ConsumerService.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

var builder = Host.CreateDefaultBuilder(args)
	.UseContentRoot(AppContext.BaseDirectory)
	.ConfigureServices((context, services) =>
	{
		BsonSerializer.RegisterSerializer(new
			GuidSerializer(GuidRepresentation.Standard));
		var mongoConnectionString = context.Configuration.GetConnectionString("MongoDB")
			?? throw new InvalidOperationException(
				"ConnectionStrings:MongoDB is not configured.");
		var mongoSettings = MongoClientSettings.FromConnectionString(mongoConnectionString);
		services.AddSingleton<IMongoClient>(new MongoClient(mongoSettings));
		services.AddSingleton<StudyGroupCreatedHandler>();
		services.AddHostedService<Worker>();
	});
await builder.RunConsoleAsync();