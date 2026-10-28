namespace ScheduleBot.Models;

public sealed class FileLinkOptions
{
    public bool Enabled { get; set; }
    public string PublicBaseUrl { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 60;
    public int MaxConcurrentDownloads { get; set; } = 8;
    public int RequestsPerMinute { get; set; } = 60;
    public int MaxActiveLinksPerUser { get; set; } = 100;
    public int DownloadTimeoutMinutes { get; set; } = 30;
    public const long MaxFileBytes = 20 * 1024 * 1024;
    public bool UseMtProto { get; set; }
    public int MtProtoApiId { get; set; }
    public string MtProtoApiHash { get; set; } = "";
    public string MtProtoSessionPath { get; set; } = "Data/Telegram/filelinks.session";
    public string MtProtoProxyUrl { get; set; } = "";
    public long MaxDownloadBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public long EffectiveMaxFileBytes => UseMtProto ? MaxDownloadBytes : MaxFileBytes;
    public bool HasMtProtoCredentials => MtProtoApiId > 0 && MtProtoApiHash is { Length: 32 }
        && MtProtoApiHash.All(Uri.IsHexDigit) && !string.IsNullOrWhiteSpace(MtProtoSessionPath);

    public bool HasPublicBaseUrl => Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
