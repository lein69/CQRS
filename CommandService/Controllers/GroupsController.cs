using CommandService.Models;
using CommandService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommandService.Controllers
{
	[Route("api/groups")]
	[ApiController]
	[Authorize]
	public class GroupsController : ControllerBase
	{
		private readonly IStudyGroupService _studyGroupService;

		public GroupsController(IStudyGroupService studyGroupService)
		{
			_studyGroupService = studyGroupService;
		}

		[HttpPost]
		[ProducesResponseType(typeof(StudyGroup), StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		public async Task<IActionResult> Create([FromBody] CreateStudyGroupRequest request)
		{
			var ownerDisplayName = User.FindFirst("name")?.Value;
			if (!TryGetUserId(out var ownerId) ||
				string.IsNullOrWhiteSpace(ownerDisplayName))
			{
				return Unauthorized();
			}

			var group = await _studyGroupService.CreateAsync(
				request,
				ownerId,
				ownerDisplayName);
			return StatusCode(StatusCodes.Status201Created, group);
		}

		[HttpDelete("{groupId:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Delete(Guid groupId)
		{
			if (!TryGetUserId(out var ownerId))
			{
				return Unauthorized();
			}

			try
			{
				await _studyGroupService.DeleteAsync(groupId, ownerId);
				return NoContent();
			}
			catch (StudyGroupAccessDeniedException exception)
			{
				return StatusCode(
					StatusCodes.Status403Forbidden,
					new { message = exception.Message });
			}
			catch (StudyGroupNotFoundException)
			{
				return NotFound();
			}
		}

		[HttpDelete("{groupId:guid}/members/me")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Leave(Guid groupId)
		{
			if (!TryGetUserId(out var userId))
			{
				return Unauthorized();
			}

			try
			{
				await _studyGroupService.LeaveAsync(groupId, userId);
				return NoContent();
			}
			catch (StudyGroupNotFoundException)
			{
				return NotFound();
			}
			catch (StudyGroupRuleViolationException exception)
			{
				return Conflict(new { message = exception.Message });
			}
		}

		[HttpPost("{groupId:guid}/join-requests")]
		[ProducesResponseType(typeof(JoinRequest), StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> RequestToJoin(
			Guid groupId,
			[FromBody] CreateJoinRequestRequest request)
		{
			if (!TryGetUserId(out var userId))
			{
				return Unauthorized();
			}

			try
			{
				var joinRequest = await _studyGroupService.CreateJoinRequestAsync(
					groupId,
					userId,
					request);
				return StatusCode(StatusCodes.Status201Created, joinRequest);
			}
			catch (ArgumentException exception)
			{
				return BadRequest(new { message = exception.Message });
			}
			catch (StudyGroupNotFoundException exception)
			{
				return NotFound(new { message = exception.Message });
			}
			catch (StudyGroupRuleViolationException exception)
			{
				return Conflict(new { message = exception.Message });
			}
		}

		[HttpPost("{groupId:guid}/join-requests/{requestId:guid}/approve")]
		[ProducesResponseType(typeof(JoinRequest), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public Task<IActionResult> ApproveJoinRequest(
			Guid groupId,
			Guid requestId)
		{
			return ReviewJoinRequest(groupId, requestId, approve: true);
		}

		[HttpPost("{groupId:guid}/join-requests/{requestId:guid}/reject")]
		[ProducesResponseType(typeof(JoinRequest), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public Task<IActionResult> RejectJoinRequest(
			Guid groupId,
			Guid requestId)
		{
			return ReviewJoinRequest(groupId, requestId, approve: false);
		}

		private async Task<IActionResult> ReviewJoinRequest(
			Guid groupId,
			Guid requestId,
			bool approve)
		{
			if (!TryGetUserId(out var ownerId))
			{
				return Unauthorized();
			}

			try
			{
				var joinRequest = await _studyGroupService.ReviewJoinRequestAsync(
					groupId,
					requestId,
					ownerId,
					approve);
				return Ok(joinRequest);
			}
			catch (ArgumentException exception)
			{
				return BadRequest(new { message = exception.Message });
			}
			catch (StudyGroupAccessDeniedException exception)
			{
				return StatusCode(
					StatusCodes.Status403Forbidden,
					new { message = exception.Message });
			}
			catch (StudyGroupNotFoundException exception)
			{
				return NotFound(new { message = exception.Message });
			}
			catch (JoinRequestNotFoundException exception)
			{
				return NotFound(new { message = exception.Message });
			}
			catch (StudyGroupRuleViolationException exception)
			{
				return Conflict(new { message = exception.Message });
			}
		}

		private bool TryGetUserId(out Guid userId)
		{
			return Guid.TryParse(User.FindFirst("sub")?.Value, out userId);
		}
	}
}
