namespace QueryService.Models;

/// <summary>Groups and pending join requests associated with the authenticated user.</summary>
public sealed record MyStudyGroupsResponse(
	IReadOnlyList<StudyGroupReadModel> OwnedGroups,
	IReadOnlyList<StudyGroupReadModel> JoinedGroups,
	IReadOnlyList<MyPendingJoinRequestResponse> PendingRequests,
	long PendingReviewCount);

/// <summary>A pending join request and its current group summary.</summary>
public sealed record MyPendingJoinRequestResponse(
	JoinRequestReadModel Request,
	StudyGroupReadModel? Group);
