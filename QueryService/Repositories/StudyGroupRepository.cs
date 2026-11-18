using MongoDB.Bson;
using MongoDB.Driver;
using QueryService.Models;
using System.Text.RegularExpressions;

namespace QueryService.Repositories
{
	public class StudyGroupRepository : IStudyGroupRepository
	{
		private readonly IMongoCollection<StudyGroupReadModel> _groups;
		private readonly IMongoCollection<JoinRequestReadModel> _joinRequests;

		public StudyGroupRepository(IMongoClient client)
		{
			var database = client.GetDatabase("study_group_read_db");
			_groups = database.GetCollection<StudyGroupReadModel>("study_groups");
			_joinRequests = database.GetCollection<JoinRequestReadModel>("join_requests");
		}

		public async Task<(List<StudyGroupReadModel> Items, long TotalCount)> SearchAsync(
			string? search,
			int page,
			int pageSize)
		{
			var filter = Builders<StudyGroupReadModel>.Filter.Empty;
			if (!string.IsNullOrWhiteSpace(search))
			{
				var pattern = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");
				filter = Builders<StudyGroupReadModel>.Filter.Or(
					Builders<StudyGroupReadModel>.Filter.Regex(group => group.Name, pattern),
					Builders<StudyGroupReadModel>.Filter.Regex(group => group.Subject, pattern),
					Builders<StudyGroupReadModel>.Filter.Regex(group => group.Goal, pattern),
					Builders<StudyGroupReadModel>.Filter.Regex(group => group.Description, pattern));
			}

			var items = await _groups.Find(filter)
				.SortByDescending(group => group.CreatedAtUtc)
				.Skip((page - 1) * pageSize)
				.Limit(pageSize)
				.ToListAsync();
			var totalCount = await _groups.CountDocumentsAsync(filter);
			return (items, totalCount);
		}

		public async Task<StudyGroupReadModel?> GetByIdAsync(Guid id)
		{
			return await _groups.Find(group => group.Id == id).FirstOrDefaultAsync();
		}

		public async Task<MyStudyGroupsResponse> GetMyGroupsAsync(
			Guid userId,
			CancellationToken cancellationToken)
		{
			var ownedGroupsTask = _groups
				.Find(group => group.OwnerId == userId)
				.SortByDescending(group => group.CreatedAtUtc)
				.ToListAsync(cancellationToken);
			var userRequestsTask = _joinRequests
				.Find(request =>
					request.UserId == userId &&
					(request.Status == "Approved" || request.Status == "Pending"))
				.SortByDescending(request => request.CreatedAtUtc)
				.ToListAsync(cancellationToken);
			await Task.WhenAll(ownedGroupsTask, userRequestsTask);

			var ownedGroups = ownedGroupsTask.Result;
			var ownedGroupIds = ownedGroups.Select(group => group.Id).ToList();
			var pendingReviewCount = ownedGroupIds.Count == 0
				? 0
				: await _joinRequests.CountDocumentsAsync(
					request =>
						ownedGroupIds.Contains(request.GroupId) &&
						request.Status == "Pending",
					cancellationToken: cancellationToken);
			var userRequests = userRequestsTask.Result;
			var approvedGroupIds = userRequests
				.Where(request => request.Status == "Approved")
				.Select(request => request.GroupId)
				.Distinct()
				.ToList();
			var joinedGroups = approvedGroupIds.Count == 0
				? new List<StudyGroupReadModel>()
				: await _groups
					.Find(group =>
						group.OwnerId != userId &&
						approvedGroupIds.Contains(group.Id))
					.SortByDescending(group => group.CreatedAtUtc)
					.ToListAsync(cancellationToken);

			var pendingRequests = userRequests
				.Where(request => request.Status == "Pending")
				.ToList();
			var pendingGroupIds = pendingRequests
				.Select(request => request.GroupId)
				.Distinct()
				.ToList();
			var pendingGroups = pendingGroupIds.Count == 0
				? new List<StudyGroupReadModel>()
				: await _groups
					.Find(group => pendingGroupIds.Contains(group.Id))
					.ToListAsync(cancellationToken);
			var groupsById = pendingGroups.ToDictionary(group => group.Id);

			return new MyStudyGroupsResponse(
				ownedGroups,
				joinedGroups,
				pendingRequests.Select(request => new MyPendingJoinRequestResponse(
					request,
					groupsById.TryGetValue(request.GroupId, out var group) ? group : null))
					.ToList(),
				pendingReviewCount);
		}

		public async Task<List<JoinRequestReadModel>> GetJoinRequestsAsync(
			Guid groupId,
			string? status)
		{
			var filter = Builders<JoinRequestReadModel>.Filter.Eq(
				request => request.GroupId,
				groupId);
			if (status is not null)
			{
				filter &= Builders<JoinRequestReadModel>.Filter.Eq(
					request => request.Status,
					status);
			}

			return await _joinRequests.Find(filter)
				.SortByDescending(request => request.CreatedAtUtc)
				.ToListAsync();
		}
	}
}
