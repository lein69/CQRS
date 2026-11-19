using CommandService.Models;

namespace CommandService.Services
{
	public interface IStudyGroupService
	{
		Task<StudyGroup> CreateAsync(
			CreateStudyGroupRequest request,
			Guid ownerId,
			string ownerDisplayName);
		Task DeleteAsync(Guid groupId, Guid ownerId);
		Task LeaveAsync(Guid groupId, Guid userId);
		Task<JoinRequest> CreateJoinRequestAsync(
			Guid groupId,
			Guid userId,
			CreateJoinRequestRequest request);
		Task<JoinRequest> ReviewJoinRequestAsync(
			Guid groupId,
			Guid requestId,
			Guid ownerId,
			bool approve);
		Task InitializeDatabaseAsync();
	}
}
