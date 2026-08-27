using System.Collections.Generic;

namespace ScheduleBot.Models;

public class UpdateData
{
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public int MessageId { get; set; }
    public bool IsCallback { get; set; }
    public bool DeleteCallback { get; set; }
    public string? CallbackData { get; set; }
    public string? MessageText { get; set; }
    public bool IsReplied { get; set; }
    public string? RepliedMessage { get; set; }
    public string? Command { get; set; }
    public string? ExistedProductName { get; set; }
    public List<string> DataSeparated { get; set; } = new();
    public List<string> MessageSeparated { get; set; } = new();
    public List<string> ReplyMessageSeparated { get; set; } = new();
    public ImportedFile? Document { get; set; }
}

public class ImportedFile(string fileAddress)
{
    public string? FileAddress { get; set; } = fileAddress;
}

public class DatePicker
{
    public long ChatId { get; set; }
    public bool IsJalali { get; set; }
    public string Method { get; set; }
    public string Message { get; set; }
    public DateTime FixedDate { get; set; }
    public int? YearLevel { get; set; }
}