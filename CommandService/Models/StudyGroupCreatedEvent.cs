namespace CommandService.Models
{
	public class StudyGroupCreatedEvent
	{
		public Guid EventId { get; set; }
		public string EventType { get; set; } = "StudyGroupCreated";
		public Guid AggregateId { get; set; }
		public StudyGroupCreatedData Data { get; set; } = new();
		public DateTime CreatedAtUtc { get; set; }
	}

	public class StudyGroupCreatedData
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Goal { get; set; } = string.Empty;
		public string Subject { get; set; } = string.Empty;
		public string Schedule { get; set; } = string.Empty;
		public string MeetingMode { get; set; } = "Online";
		public string MeetingLocation { get; set; } = string.Empty;
		public string Level { get; set; } = "All";
		public Guid OwnerId { get; set; }
		public int MaxMembers { get; set; }
		public string Status { get; set; } = string.Empty;
		public DateTime CreatedAtUtc { get; set; }
	}
}
