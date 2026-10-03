namespace ScheduleBot.Models;

public class ScoreGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid CreatorId { get; set; }
    public Guid InvitationCode { get; set; } = Guid.NewGuid();
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public class ScoreMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
}

public enum ScoreRequestKind { Ask, Give, Bank }
public enum ScoreRequestStatus { Pending, Accepted, Rejected }

// Accepted requests form the ledger. A null source denotes newly issued bank points.
public class ScoreRequest
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid InitiatorId { get; set; }
    public Guid? FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public int Amount { get; set; }
    public ScoreRequestKind Kind { get; set; }
    public ScoreRequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public class ScoreApproval
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid UserId { get; set; }
    public bool? Accepted { get; set; }
}

public class ScoreCounterException(string message) : Exception(message);
