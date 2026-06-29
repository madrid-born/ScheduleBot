using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class Cart
{
    [Key]
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid CreatorId { get; set; }
}

public class CartItem
{
    [Key]
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public string? Name { get; set; }
}

public class CartAccess
{
    [Key]
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Guid UserId { get; set; }
}