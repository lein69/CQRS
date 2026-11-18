namespace CommandService.Models
{
	public enum JoinRequestStatus
	{
		Pending,
		Approved,
		Rejected,
		Cancelled
	}

	public class JoinRequest
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid GroupId { get; set; }
		public Guid UserId { get; set; }
		public string Message { get; set; } = string.Empty;
		public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
		public Guid? ReviewedBy { get; set; }
		public DateTime? ReviewedAtUtc { get; set; }
	}
}
