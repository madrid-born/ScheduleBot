using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public sealed class FileLink
{
    [Key, MaxLength(64)] public string TokenHash { get; set; } = "";
    [MaxLength(512)] public string FileId { get; set; } = "";
    [MaxLength(512)] public string FileUniqueId { get; set; } = "";
    [MaxLength(200)] public string FileName { get; set; } = "";
    [MaxLength(150)] public string ContentType { get; set; } = "";
    public long? FileSize { get; set; }
    public long OwnerId { get; set; }
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool Revoked { get; set; }
}

public sealed class FileLinkException(string message) : Exception(message);

public sealed class TelegramFileTransferException(string stage, string reason) : IOException("Telegram file access failed.")
{
    public string Stage { get; } = stage;
    public string Reason { get; } = reason;
}
