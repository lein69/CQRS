using CommandService.Models;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CommandService.Services
{
	public class StudyGroupService : IStudyGroupService
	{
		private readonly string _connectionString;

		public StudyGroupService(IConfiguration configuration)
		{
			_connectionString = configuration.GetConnectionString("SqlServer")
				?? throw new InvalidOperationException(
					"ConnectionStrings:SqlServer is not configured.");
		}

		public async Task InitializeDatabaseAsync()
		{
			const string sql = """
				IF OBJECT_ID(N'dbo.StudyGroups', N'U') IS NULL
				BEGIN
					CREATE TABLE dbo.StudyGroups (
						Id uniqueidentifier NOT NULL PRIMARY KEY,
						Name nvarchar(100) NOT NULL,
						Description nvarchar(1000) NOT NULL,
						Goal nvarchar(300) NOT NULL CONSTRAINT DF_StudyGroups_Goal DEFAULT N'',
						Subject nvarchar(100) NOT NULL,
						Schedule nvarchar(200) NOT NULL CONSTRAINT DF_StudyGroups_Schedule DEFAULT N'',
						MeetingMode nvarchar(20) NOT NULL CONSTRAINT DF_StudyGroups_MeetingMode DEFAULT N'Online',
						MeetingLocation nvarchar(300) NOT NULL CONSTRAINT DF_StudyGroups_MeetingLocation DEFAULT N'',
						Level nvarchar(20) NOT NULL CONSTRAINT DF_StudyGroups_Level DEFAULT N'All',
						OwnerId uniqueidentifier NOT NULL,
						MaxMembers int NOT NULL,
						Status nvarchar(20) NOT NULL,
						CreatedAtUtc datetime2 NOT NULL,
						CONSTRAINT CK_StudyGroups_MaxMembers CHECK (MaxMembers >= 1),
						CONSTRAINT CK_StudyGroups_MeetingMode CHECK (MeetingMode IN (N'Online', N'InPerson', N'Hybrid')),
						CONSTRAINT CK_StudyGroups_Level CHECK (Level IN (N'All', N'Beginner', N'Intermediate', N'Advanced')),
						CONSTRAINT CK_StudyGroups_Status CHECK (Status IN (N'Active', N'Closed'))
					);
					CREATE INDEX IX_StudyGroups_Subject_Status
						ON dbo.StudyGroups (Subject, Status);
				END;

				IF COL_LENGTH(N'dbo.StudyGroups', N'Goal') IS NULL
					ALTER TABLE dbo.StudyGroups ADD Goal nvarchar(300) NOT NULL
						CONSTRAINT DF_StudyGroups_Goal DEFAULT N'' WITH VALUES;
				IF COL_LENGTH(N'dbo.StudyGroups', N'Schedule') IS NULL
					ALTER TABLE dbo.StudyGroups ADD Schedule nvarchar(200) NOT NULL
						CONSTRAINT DF_StudyGroups_Schedule DEFAULT N'' WITH VALUES;
				IF COL_LENGTH(N'dbo.StudyGroups', N'MeetingMode') IS NULL
					ALTER TABLE dbo.StudyGroups ADD MeetingMode nvarchar(20) NOT NULL
						CONSTRAINT DF_StudyGroups_MeetingMode DEFAULT N'Online' WITH VALUES;
				IF COL_LENGTH(N'dbo.StudyGroups', N'MeetingLocation') IS NULL
					ALTER TABLE dbo.StudyGroups ADD MeetingLocation nvarchar(300) NOT NULL
						CONSTRAINT DF_StudyGroups_MeetingLocation DEFAULT N'' WITH VALUES;
				IF COL_LENGTH(N'dbo.StudyGroups', N'Level') IS NULL
					ALTER TABLE dbo.StudyGroups ADD Level nvarchar(20) NOT NULL
						CONSTRAINT DF_StudyGroups_Level DEFAULT N'All' WITH VALUES;

				IF NOT EXISTS (
					SELECT 1 FROM sys.check_constraints
					WHERE parent_object_id = OBJECT_ID(N'dbo.StudyGroups')
						AND name = N'CK_StudyGroups_MeetingMode')
					EXEC(N'ALTER TABLE dbo.StudyGroups ADD CONSTRAINT CK_StudyGroups_MeetingMode
						CHECK (MeetingMode IN (N''Online'', N''InPerson'', N''Hybrid''))');
				IF NOT EXISTS (
					SELECT 1 FROM sys.check_constraints
					WHERE parent_object_id = OBJECT_ID(N'dbo.StudyGroups')
						AND name = N'CK_StudyGroups_Level')
					EXEC(N'ALTER TABLE dbo.StudyGroups ADD CONSTRAINT CK_StudyGroups_Level
						CHECK (Level IN (N''All'', N''Beginner'', N''Intermediate'', N''Advanced''))');

				IF OBJECT_ID(N'dbo.GroupMembers', N'U') IS NULL
				BEGIN
					CREATE TABLE dbo.GroupMembers (
						Id uniqueidentifier NOT NULL PRIMARY KEY,
						GroupId uniqueidentifier NOT NULL,
						UserId uniqueidentifier NOT NULL,
						Role nvarchar(20) NOT NULL,
						JoinedAtUtc datetime2 NOT NULL,
						CONSTRAINT FK_GroupMembers_StudyGroups
							FOREIGN KEY (GroupId) REFERENCES dbo.StudyGroups(Id),
						CONSTRAINT UQ_GroupMembers_GroupId_UserId UNIQUE (GroupId, UserId),
						CONSTRAINT CK_GroupMembers_Role CHECK (Role IN (N'Owner', N'Member'))
					);
					CREATE INDEX IX_GroupMembers_UserId ON dbo.GroupMembers (UserId);
				END;

				IF OBJECT_ID(N'dbo.JoinRequests', N'U') IS NULL
				BEGIN
					CREATE TABLE dbo.JoinRequests (
						Id uniqueidentifier NOT NULL PRIMARY KEY,
						GroupId uniqueidentifier NOT NULL,
						UserId uniqueidentifier NOT NULL,
						Message nvarchar(1000) NOT NULL,
						Status nvarchar(20) NOT NULL,
						CreatedAtUtc datetime2 NOT NULL,
						ReviewedBy uniqueidentifier NULL,
						ReviewedAtUtc datetime2 NULL,
						CONSTRAINT FK_JoinRequests_StudyGroups
							FOREIGN KEY (GroupId) REFERENCES dbo.StudyGroups(Id),
						CONSTRAINT CK_JoinRequests_Status
							CHECK (Status IN (N'Pending', N'Approved', N'Rejected', N'Cancelled'))
					);
					CREATE INDEX IX_JoinRequests_GroupId_Status
						ON dbo.JoinRequests (GroupId, Status);
				END;

				IF NOT EXISTS (
					SELECT 1 FROM sys.indexes
					WHERE object_id = OBJECT_ID(N'dbo.JoinRequests')
						AND name = N'UX_JoinRequests_Pending_User')
				BEGIN
					CREATE UNIQUE INDEX UX_JoinRequests_Pending_User
						ON dbo.JoinRequests (GroupId, UserId)
						WHERE Status = N'Pending';
				END;

				IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
				BEGIN
					CREATE TABLE dbo.OutboxMessages (
						Id uniqueidentifier NOT NULL PRIMARY KEY,
						EventType nvarchar(100) NOT NULL,
						AggregateId uniqueidentifier NOT NULL,
						Payload nvarchar(max) NOT NULL,
						CreatedAtUtc datetime2 NOT NULL,
						ProcessedAtUtc datetime2 NULL
					);
					CREATE INDEX IX_OutboxMessages_Pending
						ON dbo.OutboxMessages (CreatedAtUtc)
						WHERE ProcessedAtUtc IS NULL;
				END;
				""";

			await using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync();
			await using var command = new SqlCommand(sql, connection);
			await command.ExecuteNonQueryAsync();
		}

		public async Task<StudyGroup> CreateAsync(
			CreateStudyGroupRequest request,
			Guid ownerId)
		{
			if (ownerId == Guid.Empty)
			{
				throw new ArgumentException("OwnerId must be a non-empty GUID.");
			}

			var group = new StudyGroup
			{
				Name = request.Name.Trim(),
				Description = request.Description.Trim(),
				Goal = request.Goal.Trim(),
				Subject = request.Subject.Trim(),
				Schedule = request.Schedule.Trim(),
				MeetingMode = request.MeetingMode,
				MeetingLocation = request.MeetingLocation.Trim(),
				Level = request.Level,
				OwnerId = ownerId,
				MaxMembers = request.MaxMembers
			};

			const string insertGroupSql = """
				INSERT INTO dbo.StudyGroups
					(Id, Name, Description, Goal, Subject, Schedule, MeetingMode,
						MeetingLocation, Level, OwnerId, MaxMembers, Status, CreatedAtUtc)
				VALUES
					(@Id, @Name, @Description, @Goal, @Subject, @Schedule, @MeetingMode,
						@MeetingLocation, @Level, @OwnerId, @MaxMembers, @Status, @CreatedAtUtc);
				""";
			const string insertOwnerSql = """
				INSERT INTO dbo.GroupMembers (Id, GroupId, UserId, Role, JoinedAtUtc)
				VALUES (@MemberId, @GroupId, @OwnerId, N'Owner', @JoinedAtUtc);
				""";
			const string insertOutboxSql = """
				INSERT INTO dbo.OutboxMessages
					(Id, EventType, AggregateId, Payload, CreatedAtUtc)
				VALUES
					(@Id, @EventType, @AggregateId, @Payload, @CreatedAtUtc);
				""";
			var createdEvent = new StudyGroupCreatedEvent
			{
				EventId = Guid.NewGuid(),
				AggregateId = group.Id,
				CreatedAtUtc = DateTime.UtcNow,
				Data = new StudyGroupCreatedData
				{
					Id = group.Id,
					Name = group.Name,
					Description = group.Description,
					Goal = group.Goal,
					Subject = group.Subject,
					Schedule = group.Schedule,
					MeetingMode = group.MeetingMode,
					MeetingLocation = group.MeetingLocation,
					Level = group.Level,
					OwnerId = group.OwnerId,
					MaxMembers = group.MaxMembers,
					Status = group.Status.ToString(),
					CreatedAtUtc = group.CreatedAtUtc
				}
			};
			var eventPayload = JsonSerializer.Serialize(createdEvent);

			await using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync();
			await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
			try
			{
				await using (var command = new SqlCommand(insertGroupSql, connection, transaction))
				{
					command.Parameters.AddWithValue("@Id", group.Id);
					command.Parameters.AddWithValue("@Name", group.Name);
					command.Parameters.AddWithValue("@Description", group.Description);
					command.Parameters.AddWithValue("@Goal", group.Goal);
					command.Parameters.AddWithValue("@Subject", group.Subject);
					command.Parameters.AddWithValue("@Schedule", group.Schedule);
					command.Parameters.AddWithValue("@MeetingMode", group.MeetingMode);
					command.Parameters.AddWithValue("@MeetingLocation", group.MeetingLocation);
					command.Parameters.AddWithValue("@Level", group.Level);
					command.Parameters.AddWithValue("@OwnerId", group.OwnerId);
					command.Parameters.AddWithValue("@MaxMembers", group.MaxMembers);
					command.Parameters.AddWithValue("@Status", group.Status.ToString());
					command.Parameters.AddWithValue("@CreatedAtUtc", group.CreatedAtUtc);
					await command.ExecuteNonQueryAsync();
				}

				await using (var command = new SqlCommand(insertOwnerSql, connection, transaction))
				{
					command.Parameters.AddWithValue("@MemberId", Guid.NewGuid());
					command.Parameters.AddWithValue("@GroupId", group.Id);
					command.Parameters.AddWithValue("@OwnerId", group.OwnerId);
					command.Parameters.AddWithValue("@JoinedAtUtc", group.CreatedAtUtc);
					await command.ExecuteNonQueryAsync();
				}

				await using (var command = new SqlCommand(insertOutboxSql, connection, transaction))
				{
					command.Parameters.AddWithValue("@Id", createdEvent.EventId);
					command.Parameters.AddWithValue("@EventType", createdEvent.EventType);
					command.Parameters.AddWithValue("@AggregateId", createdEvent.AggregateId);
					command.Parameters.AddWithValue("@Payload", eventPayload);
					command.Parameters.AddWithValue("@CreatedAtUtc", createdEvent.CreatedAtUtc);
					await command.ExecuteNonQueryAsync();
				}

				await transaction.CommitAsync();
				return group;
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}

		public async Task<JoinRequest> CreateJoinRequestAsync(
			Guid groupId,
			Guid userId,
			CreateJoinRequestRequest request)
		{
			if (userId == Guid.Empty)
			{
				throw new ArgumentException("UserId must be a non-empty GUID.");
			}

			await using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync();
			await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
			try
			{
				var group = await GetGroupForUpdateAsync(connection, transaction, groupId);
				if (group.Status != StudyGroupStatus.Active.ToString())
				{
					throw new StudyGroupRuleViolationException(
						"Closed groups do not accept join requests.");
				}

				if (await IsMemberAsync(connection, transaction, groupId, userId))
				{
					throw new StudyGroupRuleViolationException(
						"User is already a member of this group.");
				}

				if (await HasPendingRequestAsync(
					connection,
					transaction,
					groupId,
					userId))
				{
					throw new StudyGroupRuleViolationException(
						"User already has a pending join request for this group.");
				}

				if (await GetMemberCountAsync(connection, transaction, groupId) >= group.MaxMembers)
				{
					throw new StudyGroupRuleViolationException(
						"Study group has reached its member limit.");
				}

				var joinRequest = new JoinRequest
				{
					GroupId = groupId,
					UserId = userId,
					Message = request.Message.Trim()
				};
				const string insertSql = """
					INSERT INTO dbo.JoinRequests
						(Id, GroupId, UserId, Message, Status, CreatedAtUtc)
					VALUES
						(@Id, @GroupId, @UserId, @Message, N'Pending', @CreatedAtUtc);
					""";
				await using (var command = new SqlCommand(insertSql, connection, transaction))
				{
					command.Parameters.AddWithValue("@Id", joinRequest.Id);
					command.Parameters.AddWithValue("@GroupId", joinRequest.GroupId);
					command.Parameters.AddWithValue("@UserId", joinRequest.UserId);
					command.Parameters.AddWithValue("@Message", joinRequest.Message);
					command.Parameters.AddWithValue("@CreatedAtUtc", joinRequest.CreatedAtUtc);
					await command.ExecuteNonQueryAsync();
				}

				await InsertJoinRequestEventAsync(
					connection,
					transaction,
					"JoinRequested",
					joinRequest,
					0);
				await transaction.CommitAsync();
				return joinRequest;
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}

		public async Task<JoinRequest> ReviewJoinRequestAsync(
			Guid groupId,
			Guid requestId,
			Guid ownerId,
			bool approve)
		{
			if (ownerId == Guid.Empty)
			{
				throw new ArgumentException("OwnerId must be a non-empty GUID.");
			}

			await using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync();
			await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
			try
			{
				var group = await GetGroupForUpdateAsync(connection, transaction, groupId);
				if (group.OwnerId != ownerId)
				{
					throw new StudyGroupAccessDeniedException();
				}

				var joinRequest = await GetJoinRequestForUpdateAsync(
					connection,
					transaction,
					groupId,
					requestId);
				if (joinRequest.Status != JoinRequestStatus.Pending)
				{
					throw new StudyGroupRuleViolationException(
						"Only pending join requests can be reviewed.");
				}

				if (approve)
				{
					if (group.Status != StudyGroupStatus.Active.ToString())
					{
						throw new StudyGroupRuleViolationException(
							"Closed groups cannot approve join requests.");
					}

					if (await IsMemberAsync(
						connection,
						transaction,
						groupId,
						joinRequest.UserId))
					{
						throw new StudyGroupRuleViolationException(
							"User is already a member of this group.");
					}

					var memberCount = await GetMemberCountAsync(connection, transaction, groupId);
					if (memberCount >= group.MaxMembers)
					{
						throw new StudyGroupRuleViolationException(
							"Study group has reached its member limit.");
					}

					const string insertMemberSql = """
						INSERT INTO dbo.GroupMembers (Id, GroupId, UserId, Role, JoinedAtUtc)
						VALUES (@Id, @GroupId, @UserId, N'Member', @JoinedAtUtc);
						""";
					await using (var command = new SqlCommand(
						insertMemberSql,
						connection,
						transaction))
					{
						command.Parameters.AddWithValue("@Id", Guid.NewGuid());
						command.Parameters.AddWithValue("@GroupId", groupId);
						command.Parameters.AddWithValue("@UserId", joinRequest.UserId);
						command.Parameters.AddWithValue("@JoinedAtUtc", DateTime.UtcNow);
						await command.ExecuteNonQueryAsync();
					}

					joinRequest.Status = JoinRequestStatus.Approved;
				}
				else
				{
					joinRequest.Status = JoinRequestStatus.Rejected;
				}

				joinRequest.ReviewedBy = ownerId;
				joinRequest.ReviewedAtUtc = DateTime.UtcNow;
				const string updateSql = """
					UPDATE dbo.JoinRequests
					SET Status = @Status, ReviewedBy = @ReviewedBy, ReviewedAtUtc = @ReviewedAtUtc
					WHERE Id = @Id;
					""";
				await using (var command = new SqlCommand(updateSql, connection, transaction))
				{
					command.Parameters.AddWithValue("@Status", joinRequest.Status.ToString());
					command.Parameters.AddWithValue("@ReviewedBy", joinRequest.ReviewedBy.Value);
					command.Parameters.AddWithValue("@ReviewedAtUtc", joinRequest.ReviewedAtUtc.Value);
					command.Parameters.AddWithValue("@Id", joinRequest.Id);
					await command.ExecuteNonQueryAsync();
				}

				var eventType = approve ? "JoinRequestApproved" : "JoinRequestRejected";
				var updatedMemberCount = await GetMemberCountAsync(connection, transaction, groupId);
				await InsertJoinRequestEventAsync(
					connection,
					transaction,
					eventType,
					joinRequest,
					updatedMemberCount);
				await transaction.CommitAsync();
				return joinRequest;
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}

		private static async Task<(Guid OwnerId, int MaxMembers, string Status)> GetGroupForUpdateAsync(
			SqlConnection connection,
			SqlTransaction transaction,
			Guid groupId)
		{
			const string sql = """
				SELECT OwnerId, MaxMembers, Status
				FROM dbo.StudyGroups WITH (UPDLOCK, HOLDLOCK)
				WHERE Id = @GroupId;
				""";
			await using var command = new SqlCommand(sql, connection, transaction);
			command.Parameters.AddWithValue("@GroupId", groupId);
			await using var reader = await command.ExecuteReaderAsync();
			if (!await reader.ReadAsync())
			{
				throw new StudyGroupNotFoundException();
			}

			return (reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2));
		}

		private static async Task<JoinRequest> GetJoinRequestForUpdateAsync(
			SqlConnection connection,
			SqlTransaction transaction,
			Guid groupId,
			Guid requestId)
		{
			const string sql = """
				SELECT Id, GroupId, UserId, Message, Status, CreatedAtUtc, ReviewedBy, ReviewedAtUtc
				FROM dbo.JoinRequests WITH (UPDLOCK, HOLDLOCK)
				WHERE Id = @RequestId AND GroupId = @GroupId;
				""";
			await using var command = new SqlCommand(sql, connection, transaction);
			command.Parameters.AddWithValue("@RequestId", requestId);
			command.Parameters.AddWithValue("@GroupId", groupId);
			await using var reader = await command.ExecuteReaderAsync();
			if (!await reader.ReadAsync())
			{
				throw new JoinRequestNotFoundException();
			}

			return new JoinRequest
			{
				Id = reader.GetGuid(0),
				GroupId = reader.GetGuid(1),
				UserId = reader.GetGuid(2),
				Message = reader.GetString(3),
				Status = Enum.Parse<JoinRequestStatus>(reader.GetString(4)),
				CreatedAtUtc = reader.GetDateTime(5),
				ReviewedBy = reader.IsDBNull(6) ? null : reader.GetGuid(6),
				ReviewedAtUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
			};
		}

		private static async Task<bool> IsMemberAsync(
			SqlConnection connection,
			SqlTransaction transaction,
			Guid groupId,
			Guid userId)
		{
			const string sql = """
				SELECT COUNT(1) FROM dbo.GroupMembers
				WHERE GroupId = @GroupId AND UserId = @UserId;
				""";
			await using var command = new SqlCommand(sql, connection, transaction);
			command.Parameters.AddWithValue("@GroupId", groupId);
			command.Parameters.AddWithValue("@UserId", userId);
			return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
		}

		private static async Task<bool> HasPendingRequestAsync(
			SqlConnection connection,
			SqlTransaction transaction,
			Guid groupId,
			Guid userId)
		{
			const string sql = """
				SELECT COUNT(1) FROM dbo.JoinRequests
				WHERE GroupId = @GroupId AND UserId = @UserId AND Status = N'Pending';
				""";
			await using var command = new SqlCommand(sql, connection, transaction);
			command.Parameters.AddWithValue("@GroupId", groupId);
			command.Parameters.AddWithValue("@UserId", userId);
			return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
		}

		private static async Task<int> GetMemberCountAsync(
			SqlConnection connection,
			SqlTransaction transaction,
			Guid groupId)
		{
			const string sql = "SELECT COUNT(1) FROM dbo.GroupMembers WHERE GroupId = @GroupId;";
			await using var command = new SqlCommand(sql, connection, transaction);
			command.Parameters.AddWithValue("@GroupId", groupId);
			return Convert.ToInt32(await command.ExecuteScalarAsync());
		}

		private static async Task InsertJoinRequestEventAsync(
			SqlConnection connection,
			SqlTransaction transaction,
			string eventType,
			JoinRequest joinRequest,
			int memberCount)
		{
			var @event = new StudyGroupEvent
			{
				EventId = Guid.NewGuid(),
				EventType = eventType,
				AggregateId = joinRequest.GroupId,
				CreatedAtUtc = DateTime.UtcNow,
				Data = new StudyGroupEventData
				{
					GroupId = joinRequest.GroupId,
					RequestId = joinRequest.Id,
					UserId = joinRequest.UserId,
					Message = joinRequest.Message,
					Status = joinRequest.Status.ToString(),
					CreatedAtUtc = joinRequest.CreatedAtUtc,
					ReviewedBy = joinRequest.ReviewedBy,
					ReviewedAtUtc = joinRequest.ReviewedAtUtc,
					MemberCount = memberCount
				}
			};
			const string sql = """
				INSERT INTO dbo.OutboxMessages
					(Id, EventType, AggregateId, Payload, CreatedAtUtc)
				VALUES
					(@Id, @EventType, @AggregateId, @Payload, @CreatedAtUtc);
				""";
			await using var command = new SqlCommand(sql, connection, transaction);
			command.Parameters.AddWithValue("@Id", @event.EventId);
			command.Parameters.AddWithValue("@EventType", @event.EventType);
			command.Parameters.AddWithValue("@AggregateId", @event.AggregateId);
			command.Parameters.AddWithValue("@Payload", JsonSerializer.Serialize(@event));
			command.Parameters.AddWithValue("@CreatedAtUtc", @event.CreatedAtUtc);
			await command.ExecuteNonQueryAsync();
		}

	}
}
