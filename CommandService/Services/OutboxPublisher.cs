using Confluent.Kafka;
using Microsoft.Data.SqlClient;

namespace CommandService.Services
{
	public class OutboxPublisher : BackgroundService
	{
		private const string Topic = "study-group-events";
		private readonly string _connectionString;
		private readonly ILogger<OutboxPublisher> _logger;
		private readonly IProducer<string, string> _producer;

		public OutboxPublisher(IConfiguration configuration, ILogger<OutboxPublisher> logger)
		{
			_connectionString = configuration.GetConnectionString("SqlServer")
				?? throw new InvalidOperationException(
					"ConnectionStrings:SqlServer is not configured.");
			var bootstrapServers = configuration["Kafka:BootstrapServers"]
				?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured.");
			_logger = logger;
			_producer = new ProducerBuilder<string, string>(
				new ProducerConfig { BootstrapServers = bootstrapServers }).Build();
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var pending = await GetNextPendingAsync(stoppingToken);
					if (pending is null)
					{
						await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
						continue;
					}

					await _producer.ProduceAsync(
						Topic,
						new Message<string, string>
						{
							Key = pending.Value.AggregateId.ToString(),
							Value = pending.Value.Payload
						},
						stoppingToken);
					await MarkProcessedAsync(pending.Value.Id, stoppingToken);
					_logger.LogInformation(
						"Published outbox event {EventId} to {Topic}.",
						pending.Value.Id,
						Topic);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception exception)
				{
					_logger.LogError(exception, "Failed to publish a study group outbox event.");
					await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
				}
			}
		}

		public override void Dispose()
		{
			_producer.Flush(TimeSpan.FromSeconds(5));
			_producer.Dispose();
			base.Dispose();
		}

		private async Task<(Guid Id, Guid AggregateId, string Payload)?> GetNextPendingAsync(
			CancellationToken cancellationToken)
		{
			const string sql = """
				SELECT TOP (1) Id, AggregateId, Payload
				FROM dbo.OutboxMessages
				WHERE ProcessedAtUtc IS NULL
				ORDER BY CreatedAtUtc;
				""";

			await using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);
			await using var command = new SqlCommand(sql, connection);
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
			{
				return null;
			}

			return (reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2));
		}

		private async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken)
		{
			const string sql = """
				UPDATE dbo.OutboxMessages
				SET ProcessedAtUtc = @ProcessedAtUtc
				WHERE Id = @Id AND ProcessedAtUtc IS NULL;
				""";

			await using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);
			await using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@Id", id);
			command.Parameters.AddWithValue("@ProcessedAtUtc", DateTime.UtcNow);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
}
