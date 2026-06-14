using System;
using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class CycleDetail
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int? CycleLength { get; set; } = 28;
    public int? PeriodLength { get; set; } = 5;
    public DateTime? LastStart { get; set; }
    public DateTime? LastEnd { get; set; }
}

public class CycleHistory
{
    [Key]
    public Guid Id { get; set; }
    public Guid CycleId { get; set; }
    public int Count { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public class CycleNotify
{
    [Key]
    public Guid Id { get; set; }
    public Guid CycleId { get; set; }
    public Guid ReceiverId { get; set; }
    public int NotifyMode { get; set; }
}