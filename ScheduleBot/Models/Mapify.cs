using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class MapifyMap
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public DateTime CreateTime { get; set; }
}

public class MapifyMapAccess
{
    [Key]
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid UserId { get; set; }
}

public class MapifyCategory
{
    [Key]
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public bool TempAdded { get; set; }
    public bool TempDeleted { get; set; }
}

public class MapifyLocation
{
    [Key]
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid AddedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Description { get; set; }
    public bool IsVisited { get; set; }
    public decimal? Score { get; set; }
    public DateTime CreateTime { get; set; }
}

public class MapifyLocationCategory
{
    [Key]
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid CategoryId { get; set; }
}

public sealed class MapifyLocationDraft
{
    public required Guid MapId { get; init; }
    public required string Name { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public string? Description { get; init; }
    public required bool IsVisited { get; init; }
    public decimal? Score { get; init; }
    public required IReadOnlyCollection<Guid> CategoryIds { get; init; }
}

public sealed class MapifyLocationSuggestion
{
    public required MapifyLocation Location { get; init; }
    public required IReadOnlyList<string> CategoryNames { get; init; }
}
