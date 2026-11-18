using QueryService.Models;

namespace QueryService.Repositories
{
	public interface IStudyGroupRepository
	{
		Task<(List<StudyGroupReadModel> Items, long TotalCount)> SearchAsync(
			string? search,
			int page,
			int pageSize);
		Task<StudyGroupReadModel?> GetByIdAsync(Guid id);
		Task<MyStudyGroupsResponse> GetMyGroupsAsync(
			Guid userId,
			CancellationToken cancellationToken);
		Task<List<JoinRequestReadModel>> GetJoinRequestsAsync(
			Guid groupId,
			string? status);
	}
}
