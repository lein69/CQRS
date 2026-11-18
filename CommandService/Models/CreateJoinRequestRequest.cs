using System.ComponentModel.DataAnnotations;

namespace CommandService.Models
{
	public sealed record CreateJoinRequestRequest
	{
		[StringLength(1000)]
		public string Message { get; init; } = string.Empty;
	}
}
