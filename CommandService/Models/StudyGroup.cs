namespace CommandService.Models
{
	public enum StudyGroupStatus
	{
		Active,
		Closed
	}

	public class StudyGroup
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Goal { get; set; } = string.Empty;
		public string Subject { get; set; } = string.Empty;
		public string Schedule { get; set; } = string.Empty;
		public string MeetingMode { get; set; } = "Online";
		public string MeetingLocation { get; set; } = string.Empty;
		public string Level { get; set; } = "All";
		public Guid OwnerId { get; set; }
		public string OwnerDisplayName { get; set; } = string.Empty;
		public int MaxMembers { get; set; }
		public StudyGroupStatus Status { get; set; } = StudyGroupStatus.Active;
		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	}
}
