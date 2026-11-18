namespace CommandService.Services
{
	public class StudyGroupNotFoundException : Exception
	{
		public StudyGroupNotFoundException() : base("Study group was not found.")
		{
		}
	}

	public class JoinRequestNotFoundException : Exception
	{
		public JoinRequestNotFoundException() : base("Join request was not found.")
		{
		}
	}

	public class StudyGroupAccessDeniedException : Exception
	{
		public StudyGroupAccessDeniedException() : base("Only the group owner can review join requests.")
		{
		}
	}

	public class StudyGroupRuleViolationException : Exception
	{
		public StudyGroupRuleViolationException(string message) : base(message)
		{
		}
	}
}
