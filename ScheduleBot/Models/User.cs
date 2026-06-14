using System;
using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }
    public long ChatId { get; set; }
    public string? Name { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public bool IsAccepted { get; set; }
}