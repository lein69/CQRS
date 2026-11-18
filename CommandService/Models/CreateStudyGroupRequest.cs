using System.ComponentModel.DataAnnotations;

namespace CommandService.Models
{
	public sealed record CreateStudyGroupRequest
	{
		[Required]
		[StringLength(100, MinimumLength = 2)]
		public required string Name { get; init; }

		[StringLength(1000)]
		public string Description { get; init; } = string.Empty;

		[StringLength(300)]
		public string Goal { get; init; } = string.Empty;

		[Required]
		[StringLength(100)]
		public required string Subject { get; init; }

		[StringLength(200)]
		public string Schedule { get; init; } = string.Empty;

		[RegularExpression("^(Online|InPerson|Hybrid)$")]
		public string MeetingMode { get; init; } = "Online";

		[StringLength(300)]
		public string MeetingLocation { get; init; } = string.Empty;

		[RegularExpression("^(All|Beginner|Intermediate|Advanced)$")]
		public string Level { get; init; } = "All";

		[Range(1, int.MaxValue)]
		public required int MaxMembers { get; init; }
	}
}
