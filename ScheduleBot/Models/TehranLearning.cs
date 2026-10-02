using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public static class TehranPlaceTypes
{
    public const string Landmark = "landmark";
    public const string Street = "street";
    public const string Square = "square";
    public const string Neighborhood = "neighborhood";
    public const string MetroStation = "metro_station";
    public const string Park = "park";
    public const string Building = "building";
    public const string Other = "other";
}

public static class TehranRelationshipTypes
{
    public const string LocatedIn = "located_in";
    public const string Near = "near";
    public const string ConnectedTo = "connected_to";
    public const string PartOf = "part_of";
    public const string SameAs = "same_as";
}

public static class TehranLearningStatuses
{
    public const string Unseen = "unseen";
    public const string Learning = "learning";
    public const string Mastered = "mastered";
}

public static class TehranQuizStatuses
{
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
}

public class Place
{
    [Key]
    public Guid Id { get; set; }
    [MaxLength(250)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string PlaceType { get; set; } = TehranPlaceTypes.Other;
    [MaxLength(100)]
    public string? ExternalId { get; set; }
    [MaxLength(30)]
    public string Source { get; set; } = "osm";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class PlaceAlias
{
    [Key]
    public Guid Id { get; set; }
    public Guid PlaceId { get; set; }
    [MaxLength(250)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(10)]
    public string LanguageCode { get; set; } = "fa";
    [MaxLength(30)]
    public string AliasType { get; set; } = "alternate";
}

public class PlaceRelationship
{
    [Key]
    public Guid Id { get; set; }
    public Guid FromPlaceId { get; set; }
    public Guid ToPlaceId { get; set; }
    [MaxLength(40)]
    public string RelationshipType { get; set; } = TehranRelationshipTypes.Near;
    public decimal? DistanceMeters { get; set; }
    public int? Confidence { get; set; }
}

public class Lesson
{
    [Key]
    public Guid Id { get; set; }
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Description { get; set; }
    public int Position { get; set; }
    public int Difficulty { get; set; } = 1;
    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class LessonPlace
{
    [Key]
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public Guid PlaceId { get; set; }
    public int Position { get; set; }
}

public class UserPlaceLearningProgress
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlaceId { get; set; }
    public bool IsLearnt { get; set; }
    public DateTime? LearntAtUtc { get; set; }
    [MaxLength(20)]
    public string Status { get; set; } = TehranLearningStatuses.Unseen;
    public int Attempts { get; set; }
    public int CorrectAnswers { get; set; }
    public DateTime? LastAnsweredAtUtc { get; set; }
    public DateTime? NextReviewAtUtc { get; set; }
}

public class Quiz
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? NightId { get; set; }
    [MaxLength(20)]
    public string Status { get; set; } = TehranQuizStatuses.InProgress;
    public int Score { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class QuizQuestion
{
    [Key]
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Guid PlaceId { get; set; }
    public Guid? ReferencePlaceId { get; set; }
    [MaxLength(40)]
    public string QuestionType { get; set; } = "identify_highlighted_road";
    [MaxLength(1000)]
    public string Prompt { get; set; } = string.Empty;
    public int Position { get; set; }
}

public class QuizQuestionOption
{
    [Key]
    public Guid Id { get; set; }
    public Guid QuizQuestionId { get; set; }
    [MaxLength(250)]
    public string Text { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsCorrect { get; set; }
}

public class QuizAnswer
{
    [Key]
    public Guid Id { get; set; }
    public Guid QuizQuestionId { get; set; }
    public Guid UserId { get; set; }
    public Guid SelectedOptionId { get; set; }
    public bool IsCorrect { get; set; }
    public int ScoreAwarded { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
}

public class ScoreTransaction
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    [MaxLength(40)]
    public string Type { get; set; } = "quiz_answer";
    public Guid? ReferenceId { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class TehranGameProfile
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int TotalScore { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
}

public class TehranLearningNight
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public Guid FirstPlaceId { get; set; }
    public Guid SecondPlaceId { get; set; }
    public int LearntCount { get; set; }
    public bool IsCompleted { get; set; }
}
