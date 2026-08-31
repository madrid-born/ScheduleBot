using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime StartTime { get; set; }
    public int Type { get; set; }
    public int? SeparationValue { get; set; }
    public string Name { get; set; }
    public string Message { get; set; }
}

public class NotificationAccess
{
    [Key]
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }

}

public class Future
{
    [Key]
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public DateTime Time { get; set; }
    public string? Message { get; set; }
}

public class ToBeSentNotification
{
    public Guid FutureNotificationId { get; set; }
    public long ChatId { get; set; }
    public DateTime Time { get; set; }
    public string Message { get; set; }
}