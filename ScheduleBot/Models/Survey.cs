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
    public List<SurveyQuestionDefinition> Questions { get; set; } = [];
}

public sealed class SurveyQuestionDefinition
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = SurveyTypes.Text;
    public List<string> Options { get; set; } = [];
    public string? RightAnswer { get; set; }
}
