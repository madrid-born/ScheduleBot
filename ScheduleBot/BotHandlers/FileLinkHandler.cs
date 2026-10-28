using Microsoft.Extensions.Options;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ScheduleBot.BotHandlers;

public class FileLinkHandler(
    MainService services,
    UserSessionService sessionService,
    FileLinkService fServices,
    IOptions<FileLinkOptions> options,
    ILogger<FileLinkHandler> logger)
{
    #region Handle

    public async Task HandleSection(UpdateData data)
    {
        if (!options.Value.Enabled) return;
        if (!await CheckPrivateChat(data)) return;

        sessionService.ClearSession(data.ChatId);
        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.CreateFileLink, CallBacks.CreateFileLink)],
            [new(Messages.RevokeFileLink, CallBacks.RevokeFileLink)]
        ];
        var keyboard = services.CreateKeyboard(inlineCollection: collection,
            callBackStart: $"{CallBacks.FileLinks}|{CallBacks.MainSection}|");
        await services.SendMessage(data.ChatId, Messages.FileLinksWelcome, replyMarkup: keyboard);
    }

    public async Task HandleCallBack(UpdateData data)
    {
        if (!options.Value.Enabled || !await CheckPrivateChat(data)) return;
        if (data.DataSeparated.ElementAtOrDefault(1) != CallBacks.MainSection) return;

        string prompt;
        switch (data.DataSeparated.ElementAtOrDefault(2))
        {
            case CallBacks.CreateFileLink:
                sessionService.SetData(data.ChatId, Actions.CreateFileLink);
                prompt = Messages.AskFileLinkFile;
                break;
            case CallBacks.RevokeFileLink:
                sessionService.SetData(data.ChatId, Actions.RevokeFileLink);
                prompt = Messages.AskFileLinkToRevoke;
                break;
            default:
                return;
        }
        var keyboard = services.CreateKeyboard(normalCollection: new List<List<string>> { new() { Messages.Cancel } });
        await services.SendMessage(data.ChatId, prompt, replyMarkup: keyboard);
    }

    public async Task HandleSession(UpdateData data, CancellationToken ct = default)
    {
        if (!options.Value.Enabled) return;
        if (data.MessageText is Messages.Cancel or "/cancel")
        {
            sessionService.ClearSession(data.ChatId);
            await services.SendMessage(data.ChatId, Messages.FileLinkCancelled);
            return;
        }
        switch (sessionService.GetData(data.ChatId).Action)
        {
            case Actions.CreateFileLink:
                await CreateLink(data, data.TelegramFile ?? data.RepliedTelegramFile, ct);
                break;
            case Actions.RevokeFileLink:
                await RevokeLink(data, data.MessageText ?? "", ct);
                break;
        }
    }

    #endregion

    #region File Links

    public async Task CreateLink(UpdateData data, FileLink? file, CancellationToken ct = default)
    {
        if (!options.Value.Enabled || !await CheckPrivateChat(data)) return;
        if (file == null)
        {
            await services.SendMessage(data.ChatId, Messages.AskFileLinkFile);
            return;
        }
        try
        {
            var url = await fServices.CreateAsync(file, data.ChatId, ct);
            sessionService.ClearSession(data.ChatId);
            await services.SendMessage(data.ChatId, url, parseMode: ParseMode.None, disableLinkPreview: true, ct: ct);
        }
        catch (FileLinkException ex) { await services.SendMessage(data.ChatId, ex.Message); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("File link creation failed ({ErrorType}).", ex.GetType().Name);
            await services.SendMessage(data.ChatId, Messages.FileLinkUnavailable);
        }
    }

    public async Task RevokeLink(UpdateData data, string value, CancellationToken ct = default)
    {
        if (!options.Value.Enabled || !await CheckPrivateChat(data)) return;
        try
        {
            var revoked = await fServices.RevokeAsync(value.Trim().Split('/').Last(), data.ChatId, ct);
            if (revoked) sessionService.ClearSession(data.ChatId);
            await services.SendMessage(data.ChatId, revoked ? Messages.FileLinkRevoked : Messages.FileLinkNotFound);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("File link revocation failed ({ErrorType}).", ex.GetType().Name);
            await services.SendMessage(data.ChatId, Messages.FileLinkUnavailable);
        }
    }

    private async Task<bool> CheckPrivateChat(UpdateData data)
    {
        if (data.IsPrivateChat) return true;
        await services.SendMessage(data.ChatId, Messages.FileLinkPrivateOnly);
        return false;
    }

    public static FileLink? GetFileReference(Message message)
    {
        FileBase? file = (FileBase?)message.Document ?? (FileBase?)message.Animation ?? (FileBase?)message.Video
            ?? (FileBase?)message.Audio ?? (FileBase?)message.Voice ?? (FileBase?)message.VideoNote
            ?? (FileBase?)message.Sticker ?? message.Photo?.MaxBy(x => (long)x.Width * x.Height);
        if (file == null) return null;
        var name = message.Document?.FileName ?? message.Animation?.FileName ?? message.Video?.FileName ?? message.Audio?.FileName
            ?? (message.Photo != null ? "photo.jpg" : message.Voice != null ? "voice.ogg" : message.VideoNote != null ? "video.mp4" : "file");
        var mime = message.Document?.MimeType ?? message.Animation?.MimeType ?? message.Video?.MimeType
            ?? message.Audio?.MimeType ?? message.Voice?.MimeType ?? (message.Photo != null ? "image/jpeg" : "application/octet-stream");
        return new FileLink
        {
            FileId = file.FileId, FileUniqueId = file.FileUniqueId, FileSize = file.FileSize,
            ChatId = message.Chat.Id, MessageId = message.MessageId,
            FileName = FileLinkService.SafeName(name), ContentType = FileLinkService.SafeMime(mime)
        };
    }

    #endregion
}
