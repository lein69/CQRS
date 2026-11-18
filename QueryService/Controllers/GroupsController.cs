using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QueryService.Models;
using QueryService.Repositories;
using System.Security.Claims;

namespace QueryService.Controllers
{
	[Route("api/groups")]
	[ApiController]
	public class GroupsController : ControllerBase
	{
		private readonly IStudyGroupRepository _studyGroupRepository;

		public GroupsController(IStudyGroupRepository studyGroupRepository)
		{
			_studyGroupRepository = studyGroupRepository;
		}

		[HttpGet]
		public async Task<IActionResult> Search(
			[FromQuery] string? search,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 20)
		{
			if (page < 1 || pageSize < 1 || pageSize > 100)
			{
				return BadRequest(new
				{
					message = "page must be at least 1 and pageSize must be between 1 and 100."
				});
			}

			var result = await _studyGroupRepository.SearchAsync(search, page, pageSize);
			return Ok(new
			{
				items = result.Items,
				result.TotalCount,
				page,
				pageSize
			});
		}

		[HttpGet("{id:guid}")]
		public async Task<IActionResult> GetById(Guid id)
		{
			var group = await _studyGroupRepository.GetByIdAsync(id);
			return group is null ? NotFound() : Ok(group);
		}

		[HttpGet("mine")]
		[Authorize]
		[ProducesResponseType(typeof(MyStudyGroupsResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		public async Task<ActionResult<MyStudyGroupsResponse>> GetMyGroups(
			CancellationToken cancellationToken)
		{
			if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
			{
				return Unauthorized();
			}

			var groups = await _studyGroupRepository.GetMyGroupsAsync(
				userId,
				cancellationToken);
			return Ok(groups);
		}

		[HttpGet("{groupId:guid}/join-requests")]
		[Authorize]
		[ProducesResponseType(typeof(List<JoinRequestReadModel>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetJoinRequests(
			Guid groupId,
			[FromQuery] string? status = "Pending")
		{
			string? normalizedStatus = null;
			if (status is not null)
			{
				if (!Enum.TryParse<JoinRequestStatus>(
					status,
					ignoreCase: true,
					out var parsedStatus))
				{
					return BadRequest(new
					{
						message = "status must be Pending, Approved, Rejected, or Cancelled."
					});
				}

				normalizedStatus = parsedStatus.ToString();
			}

			var group = await _studyGroupRepository.GetByIdAsync(groupId);
			if (group is null)
			{
				return NotFound();
			}

			if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId))
			{
				return Unauthorized();
			}

			if (group.OwnerId != userId)
			{
				return Forbid();
			}

			var requests = await _studyGroupRepository.GetJoinRequestsAsync(
				groupId,
				normalizedStatus);
			return Ok(requests);
		}

		private enum JoinRequestStatus
		{
			Pending,
			Approved,
			Rejected,
			Cancelled
		}
	}
}
