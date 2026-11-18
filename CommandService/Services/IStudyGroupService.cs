using CommandService.Models;

namespace CommandService.Services
{
	public interface IStudyGroupService
	{
		Task<StudyGroup> CreateAsync(CreateStudyGroupRequest request, Guid ownerId);
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
