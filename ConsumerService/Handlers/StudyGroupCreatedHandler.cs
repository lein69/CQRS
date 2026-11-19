using ConsumerService.Models;
using MongoDB.Driver;

namespace ConsumerService.Handlers
{
	public class StudyGroupCreatedHandler
	{
		private readonly IMongoCollection<StudyGroupReadModel> _groups;
		private readonly IMongoCollection<JoinRequestReadModel> _joinRequests;

		public StudyGroupCreatedHandler(IMongoClient client)
		{
			var database = client.GetDatabase("study_group_read_db");
			_groups = database.GetCollection<StudyGroupReadModel>("study_groups");
			_joinRequests = database.GetCollection<JoinRequestReadModel>("join_requests");
		}

		public async Task HandleAsync(
			StudyGroupCreatedEvent @event,
			CancellationToken cancellationToken)
		{
			var group = new StudyGroupReadModel
			{
				Id = @event.Data.Id,
				Name = @event.Data.Name,
				Description = @event.Data.Description,
				Goal = @event.Data.Goal,
				Subject = @event.Data.Subject,
				Schedule = @event.Data.Schedule,
				MeetingMode = @event.Data.MeetingMode,
				MeetingLocation = @event.Data.MeetingLocation,
				Level = @event.Data.Level,
				OwnerId = @event.Data.OwnerId,
				OwnerDisplayName = @event.Data.OwnerDisplayName,
				MaxMembers = @event.Data.MaxMembers,
				MemberCount = 1,
				Status = @event.Data.Status,
				CreatedAtUtc = @event.Data.CreatedAtUtc
			};

			var filter = Builders<StudyGroupReadModel>.Filter.Eq(item => item.Id, group.Id);
			await _groups.ReplaceOneAsync(
				filter,
				group,
				new ReplaceOptions { IsUpsert = true },
				cancellationToken);
		}

		public async Task HandleAsync(
			StudyGroupEvent @event,
			CancellationToken cancellationToken)
		{
			if (@event.EventType == "GroupDeleted")
			{
				await _groups.DeleteOneAsync(
					group => group.Id == @event.Data.GroupId,
					cancellationToken);
				await _joinRequests.DeleteManyAsync(
					request => request.GroupId == @event.Data.GroupId,
					cancellationToken);
				return;
			}

			if (@event.EventType == "StudyGroupOwnerUpdated")
			{
				await _groups.UpdateOneAsync(
					group => group.Id == @event.Data.GroupId,
					Builders<StudyGroupReadModel>.Update.Set(
						group => group.OwnerDisplayName,
						@event.Data.OwnerDisplayName),
					cancellationToken: cancellationToken);
				return;
			}

			if (@event.EventType == "MemberLeft")
			{
				await _joinRequests.UpdateManyAsync(
					request =>
						request.GroupId == @event.Data.GroupId &&
						request.UserId == @event.Data.UserId &&
						request.Status == "Approved",
					Builders<JoinRequestReadModel>.Update.Set(
						request => request.Status,
						"Cancelled"),
					cancellationToken: cancellationToken);
				await _groups.UpdateOneAsync(
					group => group.Id == @event.Data.GroupId,
					Builders<StudyGroupReadModel>.Update.Set(
						group => group.MemberCount,
						@event.Data.MemberCount),
					cancellationToken: cancellationToken);
				return;
			}

			var request = new JoinRequestReadModel
			{
				Id = @event.Data.RequestId,
				GroupId = @event.Data.GroupId,
				UserId = @event.Data.UserId,
				Message = @event.Data.Message,
				Status = @event.Data.Status,
				CreatedAtUtc = @event.Data.CreatedAtUtc,
				ReviewedBy = @event.Data.ReviewedBy,
				ReviewedAtUtc = @event.Data.ReviewedAtUtc
			};
			var requestFilter = Builders<JoinRequestReadModel>.Filter.Eq(
				item => item.Id,
				request.Id);
			await _joinRequests.ReplaceOneAsync(
				requestFilter,
				request,
				new ReplaceOptions { IsUpsert = true },
				cancellationToken);

			if (@event.EventType == "JoinRequestApproved")
			{
				var groupFilter = Builders<StudyGroupReadModel>.Filter.Eq(
					group => group.Id,
					@event.Data.GroupId);
				await _groups.UpdateOneAsync(
					groupFilter,
					Builders<StudyGroupReadModel>.Update.Set(
						group => group.MemberCount,
						@event.Data.MemberCount),
					cancellationToken: cancellationToken);
			}
		}
	}
}
