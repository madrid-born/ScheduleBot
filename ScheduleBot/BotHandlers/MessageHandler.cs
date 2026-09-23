using System.Text.RegularExpressions;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class MessageHandler(
    ITelegramBotClient bot,
    UserHandler userHandler,
    CycleTrackerHandler cycleTrackerHandler,
    UserSessionService sessionService,
    CartHandler cartHandler,
    TransactionHandler transactionHandler,
    SpotifyHandler spotifyHandler,
    NotificationHandler notificationHandler,
    MetroHandler metroHandler,
    MapifyHandler mapifyHandler,
    SurveyHandler surveyHandler,
    MainService services,
    IConfiguration configuration)
{
    public async Task HandleUpdateAsync(ITelegramBotClient bot1, Update update, CancellationToken ct)
    {
        long chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        var surveyUpload = update.Message?.Document != null && sessionService.GetOrSetData(chatId).Action == SurveyHandler.SessionAction;
        UpdateData? updateData = null;
        try
        {
            updateData = await ExtractUpdateDataAsync(update);
            chatId = updateData.ChatId;
            if (update.CallbackQuery != null && updateData.DataSeparated.FirstOrDefault() == CallBacks.Survey)
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
            if (!await userHandler.CheckUserStatusAsync(updateData)) return;
            if (updateData.IsCallback && !string.IsNullOrEmpty(updateData.CallbackData))
                await HandleCallbackAsync(updateData);
            else
                await HandleMessageAsync(updateData);
        }
        catch (IOException ex)
        {
            await services.SendMessage(chatId, ex.Message);
        }
        catch (Exception ex)
        {
            var adminChatId = long.Parse(configuration["Telegram:AdminChatId"]!);

            await services.SendMessage(chatId, Messages.SomethingWentWrong);
            if (chatId == adminChatId) await services.SendMessage(chatId, ex.Message);
        }
        finally
        {
            if (updateData?.Document?.FileAddress is { } path && surveyUpload)
                File.Delete(path);
        }
    }

    private async Task<UpdateData> ExtractUpdateDataAsync(Update update)
    {
        var data = new UpdateData();
        
        if (update.CallbackQuery != null)
        {
            data.IsCallback = true;
            data.DeleteCallback = true;
            data.ChatId = update.CallbackQuery.Message!.Chat.Id;
            data.Username = update.CallbackQuery.Message!.Chat.Username;
            data.MessageId = update.CallbackQuery.Message.MessageId;
            data.CallbackData = update.CallbackQuery.Data;
            var textData = update.CallbackQuery.Data ?? "";
            if (textData.StartsWith('*'))
            {
                textData = textData[1..];
                data.DeleteCallback = false;
            }
            data.DataSeparated = textData.Split("|").ToList();
            if (update.CallbackQuery.Message.Text == null) return data;
            data.MessageText = update.CallbackQuery.Message.Text;
            data.MessageSeparated = (update.CallbackQuery.Message.Text ?? "").Split('"').ToList();
        }
        else if (update.Message != null)
        {
            data.ChatId = update.Message.Chat.Id;
            data.Username = update.Message.Chat.Username;
            data.MessageId = update.Message.MessageId;
            data.MessageText = update.Message.Text;
            data.MessageSeparated = (update.Message.Text ?? "").Split('"').ToList();
            if (update.Message.Location != null)
            {
                data.Latitude = update.Message.Location.Latitude;
                data.Longitude = update.Message.Location.Longitude;
            }

            if (update.Message.Document != null)
            {
                if (sessionService.GetOrSetData(data.ChatId).Action == SurveyHandler.SessionAction &&
                    update.Message.Document.FileSize > SurveyService.MaxJsonBytes)
                    throw new IOException("Survey files must be at most 256 KB.");
                data.DocumentName = update.Message.Document.FileName;
                data.MessageText = update.Message.Caption;
                var extension = (Path.GetExtension(data.DocumentName) ?? "").TrimStart('.');
                data.Document = new ImportedFile(await LoadFile(update.Message.Document, extension));
            }
            
            data.Command = update.Message.Text;
            
            if (update.Message.ReplyToMessage == null) return data;
            data.IsReplied = true;
            data.RepliedMessage = update.Message.ReplyToMessage.Text;
            data.RepliedMessageId = update.Message.ReplyToMessage.MessageId;
            
            data.ReplyMessageSeparated = (update.Message.ReplyToMessage.Text ?? "").Split('\n').ToList();
            if (data.ReplyMessageSeparated.Count == 0) data.ReplyMessageSeparated.Add(update.Message.ReplyToMessage.Text!);

            if (update.Message.ReplyToMessage.Text != "Enter the name of the product") return data;
            data.Command = "AddItem";
            data.ExistedProductName = update.Message.Text;
        }
        
        return data;
    }

    private async Task<string> LoadFile(Document document, string format)
    {
        var file = await bot.GetFile(document.FileId);
        var fileAddress = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{format}");

        await using var fs = File.Create(fileAddress);
        await bot.DownloadFile(file.FilePath!, fs);
        return fileAddress;
    }

    private async Task HandleCallbackAsync(UpdateData data)
    {
        if (data.DeleteCallback) await services.DeleteMessage(data.ChatId, data.MessageId);

        switch (data.DataSeparated[0])
        {
            case CallBacks.MainSection:
                await services.HandleCallBack(data);
                break;
            case CallBacks.Register:
                await userHandler.HandleCallBack(data);
                break;
            case CallBacks.Cycle:
                await cycleTrackerHandler.HandleCallBack(data);
                break;
            case CallBacks.Cart:
                await cartHandler.HandleCallBack(data);
                break;
            case CallBacks.Transaction:
                await transactionHandler.HandleCallBack(data);
                break;
            case CallBacks.Spotify:
                await spotifyHandler.HandleCallBack(data);
                break;
            case CallBacks.Notification:
                await notificationHandler.HandleCallBack(data);
                break;
            case CallBacks.Metro:
                await metroHandler.HandleCallBack(data);
                break;
            case CallBacks.Mapify:
                await mapifyHandler.HandleCallBack(data);
                break;
            case CallBacks.Survey:
                await surveyHandler.HandleCallBack(data);
                break;
        }
    }

    private async Task<bool> CheckDocument(UpdateData data)
    {
        if (data.Document == null) return false;
        if (sessionService.GetOrSetData(data.ChatId).Action == SurveyHandler.SessionAction)
        {
            await surveyHandler.HandleSession(data);
            return true;
        }
        try
        {
            var result = await CheckSession(data);
            if (result) return true;
        }
        catch (Exception e) { /*ignored*/ }
        
        var session = sessionService.GetOrSetData(data.ChatId);
        if (Regex.IsMatch(data.DocumentName ?? "", Pattern.BluPattern) && Regex.IsMatch(data.MessageText ?? "", "Share " + Pattern.BluPattern))
        {
            session.SetAction(Actions.AwaitingBluFile);
            var walletId = await transactionHandler.SelectWalletForFile(data.ChatId);
            if (walletId == Guid.Empty)
            {
                session.SetContext(Context.FileAddress, data.Document!.FileAddress);
                return true;
            }
            session.SetCallBack(walletId.ToString()!);
        }
        return await CheckSession(data);
    }

    private async Task HandleMessageAsync(UpdateData data)
    {
        if (await CheckDocument(data)) return;
        if (await CheckReplied(data)) return;
        if (await CheckCommand(data)) return;
        if (await CheckKeyboard(data)) return;
        if (await CheckSession(data)) return;
        await services.SendMessage(data.ChatId, Messages.NotFound);
    }

    private async Task<bool> CheckReplied(UpdateData data)
    {
        var flag = false;
        if (!data.IsReplied) return flag;
        switch (data.RepliedMessage)
        {
            // cart
            // case Messages.AskCartName:
            //     await cartHandler.CreateCart(data);
            //     flag = true;
            //     break;
            // case Messages.AskCartId:
            //     await cartHandler.JoinToCart(data);
            //     flag = true;
            //     break;
            //Transaction
            case Messages.AskWalletName:
                await transactionHandler.CreateWallet(data);
                flag = true;
                break;
        }
        return flag;
    }

    private async Task<bool> CheckCommand(UpdateData data)
    {
        var flag = false;
        var text = data.MessageText;
        if (string.IsNullOrEmpty(text) || !text.StartsWith('/')) return flag;
        if (text.StartsWith("/Test") && data.ChatId == services.AdminChatId)
        {
            await services.SendMessage(data.ChatId, "Nothing in here");
            flag = true;
        }
        if (text.StartsWith(Messages.Start))
        {
            var parts = data.MessageText!.Split(" ").ToList();
            if (parts.Count > 1)
            {
                var splitter = parts[1].Split("_").ToList();
                switch (splitter[0])
                {
                    case CallBacks.Survey:
                    {
                        if (splitter.Count == 3 && splitter[1] == "join")
                        {
                            data.MessageText = splitter[2];
                            await surveyHandler.JoinSurveyByCode(data);
                            flag = true;
                        }
                        break;
                    }
                    case CallBacks.Cart:
                    {
                        switch (splitter[1])
                        {
                            case CallBacks.JoinToCart:
                            {
                                data.MessageText = splitter[2];
                                await cartHandler.JoinToCart(data);
                                flag = true;
                                break;
                            }
                        }
                        break;
                    }
                    case CallBacks.Cycle:
                    {
                        switch (splitter[1])
                        {
                            case CallBacks.JoinToCycle:
                            {
                                data.MessageText = splitter[2];
                                await cycleTrackerHandler.JoinToCycleById(data);
                                flag = true;
                                break;
                            }
                        }
                        break;
                    }
                    case CallBacks.Transaction:
                    {
                        switch (splitter[1])
                        {
                            case CallBacks.JoinWallet:
                            {
                                data.MessageText = splitter[2];
                                await transactionHandler.JoinToWalletById(data);
                                flag = true;
                                break;
                            }
                        }
                        break;
                    }
                    case CallBacks.Mapify:
                    {
                        if (splitter.Count > 2 && splitter[1] == CallBacks.MapifyJoin)
                        {
                            data.MessageText = splitter[2];
                            await mapifyHandler.JoinMapById(data);
                            flag = true;
                        }
                        break;
                    }
                }
                return flag;
            }
            flag = true;
            if (sessionService.GetOrSetData(data.ChatId).Action == SurveyHandler.SessionAction)
                sessionService.ClearSession(data.ChatId);
            await services.SendMessage(data.ChatId, Messages.Welcome);
        }
        return flag;
    }

    private async Task<bool> CheckKeyboard(UpdateData data)
    {
        var flag = false;
        var keyboardSymbol = "";

        try
        {
            keyboardSymbol = data.MessageText;
        }
        catch (Exception e) { /*ignored*/ }
        
        if (keyboardSymbol is Messages.PeriodTracker or Messages.Cart or Messages.Transaction or Messages.Spotify
            or Messages.Notification or Messages.Metro or Messages.Mapify or Messages.About &&
            sessionService.GetOrSetData(data.ChatId).Action == SurveyHandler.SessionAction)
            sessionService.ClearSession(data.ChatId);

        switch (keyboardSymbol)
        {
            case Messages.PeriodTracker:
                await cycleTrackerHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Cart:
                await cartHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Transaction:
                await transactionHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Spotify:
                await spotifyHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Notification:
                await notificationHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Metro:
                await metroHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Mapify:
                await mapifyHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.Survey:
                await surveyHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.About:
                flag = true;
                break;
        }
        return flag;
    }

    private async Task<bool> CheckSession(UpdateData data)
    {
        var flag = false;
        var session = sessionService.GetOrSetData(data.ChatId);
        if (session.Action == SurveyHandler.SessionAction)
        {
            await surveyHandler.HandleSession(data);
            return true;
        }
        if (session.Timestamp.AddHours(1) < DateTime.UtcNow)
        {
            sessionService.ClearSession(data.ChatId);
            return flag;
        }

        if (data.Document != null)
        {
            switch (session.Action)
            {
                case Actions.AwaitingBluFile:
                    await transactionHandler.ProcessBluFile(data, data.Document!.FileAddress);
                    flag = true;
                    break;
            }
        }
        switch (session.Action)
        {
            case Actions.Register:
                switch (session.CallbackData)
                {
                    case SessionCallBacks.AskForName:
                        await userHandler.AskForEmail(data);
                        flag = true;
                        break;
                    case SessionCallBacks.AskForEmail:
                        await userHandler.RegisterUser(data); 
                        flag = true;
                        break;
                }
                break;
            case Actions.SetUpPeriod:
                switch (session.CallbackData)
                {
                    case SessionCallBacks.AskForCycleLength:
                        await cycleTrackerHandler.SaveCycleLength(data); 
                        flag = true;
                        break;
                    case SessionCallBacks.AskForPeriodLength:
                        await cycleTrackerHandler.SavePeriodLength(data);
                        flag = true;
                        break;
                }
                break;
            case Actions.SetUpCart:
                switch (session.CallbackData)
                {
                    case SessionCallBacks.AskCartName:
                        await cartHandler.CreateCart(data);
                        flag = true;
                        break;
                }
                break;
            case Actions.AwaitingProductActions:
                await cartHandler.AddProductToCart(data, session.CallbackData);
                flag = true;
                break;
            case Actions.AwaitingCategoryName:
                await transactionHandler.AddCategoriesToWallet(data, session.CallbackData);
                flag = true;
                break;
            case Actions.AwaitingBluReview:
                switch (session.CallbackData)
                {
                    case CallBacks.CategorySelected:
                        await transactionHandler.SetTransactionTitle(data);
                        flag = true;
                        break;
                }
                break;
            case Actions.AwaitingPlaylistId:
                await spotifyHandler.CategorizePlaylist(data, data.MessageText!);
                flag = true;
                break;
            
            case Actions.SetUpNotification:
                switch (session.CallbackData)
                {
                    case SessionCallBacks.AskNotificationName:
                        await notificationHandler.CreateNotification(data);
                        flag = true;
                        break;
                    case SessionCallBacks.AskNotificationOftenUnit:
                        await notificationHandler.SetNotificationOftenUnit(data);
                        flag = true;
                        break;
                    case SessionCallBacks.AskReminderMessage:
                        await notificationHandler.SetNotificationReminderMessage(data);
                        flag = true;
                        break;
                }
                break;
            case Actions.EditNotification:
                switch (session.CallbackData)
                {
                    case SessionCallBacks.AskEditedNotificationName:
                        await notificationHandler.EditNotificationName(data);
                        flag = true;
                        break;
                    case SessionCallBacks.AskEditedNotificationMessage:
                        await notificationHandler.EditNotificationMessage(data);
                        flag = true;
                        break;
                    case SessionCallBacks.AskEditedNextMessage:
                        await notificationHandler.EditNextMessage(data);
                        flag = true;
                        break;
                    case SessionCallBacks.AskEditedSeparationValue:
                        await notificationHandler.EditSeparationValue(data);
                        flag = true;
                        break;
                }
                break;
            case Actions.MetroNavigation:
                await metroHandler.HandleSession(data);
                flag = true;
                break;
            case Actions.MapifyCreateMap:
            case Actions.MapifyManagingCategories:
            case Actions.MapifyAddingLocation:
            case Actions.MapifyEditingLocation:
            case Actions.MapifySuggestingLocation:
                await mapifyHandler.HandleSession(data);
                flag = true;
                break;

        }
        return flag;
    }
}
