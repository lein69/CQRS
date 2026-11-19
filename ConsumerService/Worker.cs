using Confluent.Kafka;
using ConsumerService.Handlers;
using ConsumerService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConsumerService
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private readonly IConfiguration _configuration;
		private readonly StudyGroupCreatedHandler _studyGroupCreatedHandler;

		public Worker(
			ILogger<Worker> logger,
			IConfiguration configuration,
			StudyGroupCreatedHandler studyGroupCreatedHandler)
		{
			_logger = logger;
			_configuration = configuration;
			_studyGroupCreatedHandler = studyGroupCreatedHandler;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var consumerConfig = new ConsumerConfig
			{
				BootstrapServers = _configuration["Kafka:BootstrapServers"]
					?? throw new InvalidOperationException(
						"Kafka:BootstrapServers is not configured."),
				GroupId = "read-model-consumer",
				AutoOffsetReset = AutoOffsetReset.Earliest,
				EnableAutoCommit = false
			};
			using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
				.Build();
			consumer.Subscribe("study-group-events");
			while (!stoppingToken.IsCancellationRequested)
			{
				ConsumeResult<string, string>? message = null;
				try
				{
					message = consumer.Consume(stoppingToken);
					using var document = JsonDocument.Parse(message.Message.Value);
					var eventType = document.RootElement
						.GetProperty("EventType")
						.GetString();

					switch (eventType)
					{
						case "StudyGroupCreated":
							var groupEvent = JsonSerializer.Deserialize<StudyGroupCreatedEvent>(
								message.Message.Value)
								?? throw new JsonException(
									"StudyGroupCreated event payload was empty.");
							await _studyGroupCreatedHandler.HandleAsync(
								groupEvent,
								stoppingToken);
							break;
						case "JoinRequested":
						case "JoinRequestApproved":
						case "JoinRequestRejected":
						case "MemberLeft":
						case "GroupDeleted":
						case "StudyGroupOwnerUpdated":
							var studyGroupEvent = JsonSerializer.Deserialize<StudyGroupEvent>(
								message.Message.Value)
								?? throw new JsonException(
									"Study group event payload was empty.");
							await _studyGroupCreatedHandler.HandleAsync(
								studyGroupEvent,
								stoppingToken);
							break;
						default:
							_logger.LogWarning(
								"Skipping unsupported event type {EventType} from topic {Topic}.",
								eventType,
								message.Topic);
							break;
					}

					consumer.Commit(message);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing Kafka message; it will be retried.");
					if (message is not null)
					{
						consumer.Seek(message.TopicPartitionOffset);
					}

					await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
				}
			}
			consumer.Close();
		}
	}
}
