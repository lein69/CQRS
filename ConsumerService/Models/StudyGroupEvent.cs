namespace ConsumerService.Models
{
	public class StudyGroupEvent
	{
		public Guid EventId { get; set; }
		public string EventType { get; set; } = string.Empty;
		public Guid AggregateId { get; set; }
		public StudyGroupEventData Data { get; set; } = new();
		public DateTime CreatedAtUtc { get; set; }
	}

	public class StudyGroupEventData
	{
		public Guid GroupId { get; set; }
		public Guid RequestId { get; set; }
		public Guid UserId { get; set; }
		public string Message { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public string OwnerDisplayName { get; set; } = string.Empty;
		public DateTime CreatedAtUtc { get; set; }
		public Guid? ReviewedBy { get; set; }
		public DateTime? ReviewedAtUtc { get; set; }
		public int MemberCount { get; set; }
	}
}
