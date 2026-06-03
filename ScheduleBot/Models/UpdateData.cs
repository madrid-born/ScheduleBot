namespace ScheduleBot.Models;

public class UpdateData
{
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public bool IsCallback { get; set; }
    public string? CallbackData { get; set; }
    public string? MessageText { get; set; }
    public bool IsReplied { get; set; }
    public string? RepliedMessage { get; set; }
    public string? Command { get; set; }
    public string? ExistedProductName { get; set; }
    public List<string> DataSeparated { get; set; } = new();
    public List<string> MessageSeparated { get; set; } = new();
    public List<string> ReplyMessageSeparated { get; set; } = new();
}