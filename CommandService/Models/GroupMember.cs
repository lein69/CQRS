namespace CommandService.Models
{
	public enum GroupMemberRole
	{
		Owner,
		Member
	}

	public class GroupMember
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid GroupId { get; set; }
		public Guid UserId { get; set; }
		public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;
		public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
	}
}
