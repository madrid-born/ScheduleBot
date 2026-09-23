using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class Survey
{
    [Key]
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid InvitationCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsPrivate { get; set; }
}

public class SurveyAccess
{
    [Key]
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Guid UserId { get; set; }
}

public class Question
{
    [Key]
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? RightAnswer { get; set; }
    public int Position { get; set; }
    public string DataType { get; set; } = SurveyTypes.Text;
    public string? StateOneName { get; set; }
    public string? StateTwoName { get; set; }

    public IReadOnlyList<string> StateNames => StateOneName == null ? [] : [StateOneName, StateTwoName!];
    public int StateCount => StateOneName == null ? 1 : 2;
    
}

public class Answer
{
    [Key]
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public int Position { get; set; }
    public string DataType { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class UserAnswer
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SurveyId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? AnswerId { get; set; }
    public string? Value { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int StateIndex { get; set; }
}

public static class SurveyTypes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Choice = "choice";
}

/// <summary>The portable JSON format; database identifiers are never imported.</summary>
public sealed class SurveyDefinition
{
    public string Name { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public List<SurveyQuestionDefinition> Questions { get; set; } = [];
}

public sealed class SurveyQuestionDefinition
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = SurveyTypes.Text;
    public List<string> Options { get; set; } = [];
    public string? RightAnswer { get; set; }
    public List<string> States { get; set; } = [];
}

/// <summary>Portable, user-shareable answers for a private survey.</summary>
public sealed class SurveyAnswerExport
{
    public int SchemaVersion { get; set; } = 1;
    public Guid SurveyId { get; set; }
    public string SurveyName { get; set; } = string.Empty;
    public string SurveyFingerprint { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public Guid ShareId { get; set; }
    public DateTime ExportedAtUtc { get; set; }
    public List<SurveyExportAnswer> Answers { get; set; } = [];
}

public sealed class SurveyExportAnswer
{
    public int Question { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = SurveyTypes.Text;
    public string? State { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed record SurveyComparisonParticipant(string Key, string Name, List<SurveyExportAnswer> Answers);

public sealed class SurveyComparisonData
{
    public string SurveyName { get; init; } = string.Empty;
    public bool IsPrivate { get; init; }
    public List<SurveyQuestionDefinition> Questions { get; init; } = [];
    public List<SurveyComparisonParticipant> Participants { get; init; } = [];
}
