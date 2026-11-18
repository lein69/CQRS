using MongoDB.Bson.Serialization.Attributes;

namespace ConsumerService.Models
{
	public class JoinRequestReadModel
	{
		[BsonId]
		public Guid Id { get; set; }
		public Guid GroupId { get; set; }
		public Guid UserId { get; set; }
		public string Message { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public DateTime CreatedAtUtc { get; set; }
		public Guid? ReviewedBy { get; set; }
		public DateTime? ReviewedAtUtc { get; set; }
	}
}
