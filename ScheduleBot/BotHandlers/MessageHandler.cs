using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class MessageHandler(
    ITelegramBotClient bot,
    DatabaseService db,
    UserHandler userHandler,
    CycleTrackerHandler cycleTrackerHandler,
    UserSessionService sessionService,
    CartHandler cartHandler,
    MainService mainService,
    IConfiguration configuration)
{
    public async Task HandleUpdateAsync(ITelegramBotClient bot1, Update update, CancellationToken ct)
    {
        long chatId = 0;
        try
        {
            var updateData = ExtractUpdateDataAsync(update);
            chatId = updateData.ChatId;
            if (!await userHandler.CheckUserStatusAsync(updateData)) return;
            if (updateData.IsCallback && !string.IsNullOrEmpty(updateData.CallbackData))
            {
                await HandleCallbackAsync(updateData);
            }
            else
            {
                await HandleMessageAsync(updateData);
            }
        }
        catch (IOException ex)
        {
            await bot.SendMessage(chatId, ex.Message, replyMarkup: mainService.GetMainKeyboard());
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, Messages.SomethingWentWrong, replyMarkup: mainService.GetMainKeyboard());
            if (chatId == 315703198)
            {
                await bot.SendMessage(chatId, ex.Message, replyMarkup: mainService.GetMainKeyboard());
            }
        }
    }

    private static UpdateData ExtractUpdateDataAsync(Update update)
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
            data.DataSeparated = textData.Split("\\").ToList();
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

            data.Command = update.Message.Text;
            
            if (update.Message.ReplyToMessage == null) return data;
            data.IsReplied = true;
            data.RepliedMessage = update.Message.ReplyToMessage.Text;
            
            data.ReplyMessageSeparated = (update.Message.ReplyToMessage.Text ?? "").Split('\n').ToList();
            if (data.ReplyMessageSeparated.Count == 0) data.ReplyMessageSeparated.Add(update.Message.ReplyToMessage.Text!);

            if (update.Message.ReplyToMessage.Text != "Enter the name of the product") return data;
            data.Command = "AddItem";
            data.ExistedProductName = update.Message.Text;
        }
        
        return data;
    }

    private async Task HandleCallbackAsync(UpdateData data)
    {
        if (data.DeleteCallback) await bot.DeleteMessage(data.ChatId, data.MessageId);

        switch (data.DataSeparated[0])
        {
            case CallBacks.Register:
                await userHandler.HandleCallBack(data);
                break;
            case CallBacks.Cycle:
                await cycleTrackerHandler.HandleCallBack(data);
                break;
            case CallBacks.Cart:
                await cartHandler.HandleCallBack(data);
                break;
        }
    }

    private async Task HandleMessageAsync(UpdateData data)
    {
        if (await CheckReplied(data)) return;
        if (await CheckCommand(data)) return;
        if (await CheckKeyboard(data)) return;
        if (await CheckSession(data)) return;
        await bot.SendMessage(data.ChatId, Messages.NotFound, replyMarkup: mainService.GetMainKeyboard());
    }

    private async Task<bool> CheckReplied(UpdateData data)
    {
        var flag = false;
        if (!data.IsReplied) return flag;
        switch (data.RepliedMessage)
        {
            // Register
            case Messages.EnterYourName:
                await userHandler.AskForEmail(data);
                flag = true;
                break;
            case Messages.EnterYourEmail:
                await userHandler.RegisterUser(data); 
                flag = true;
                break;
            // Cycle Tracker
            case Messages.SetupTracker:
                await cycleTrackerHandler.SaveLastPeriodStart(data);
                flag = true;
                break;
            case Messages.AskForCycleLength:
                await cycleTrackerHandler.SaveCycleLength(data); 
                flag = true;
                break;
            case Messages.AskForPeriodLength:
                await cycleTrackerHandler.SavePeriodLength(data);
                flag = true;
                break;
            case Messages.AskForCycleId:
                await cycleTrackerHandler.JoinToCycleById(data);
                flag = true;
                break;
            // Cycle Tracker
            case Messages.AskCartName:
                await cartHandler.CreateCart(data);
                flag = true;
                break;
            case Messages.AskCartId:
                await cartHandler.JoinToCart(data);
                flag = true;
                break;
        }
        return flag;
    }

    private async Task<bool> CheckCommand(UpdateData data)
    {
        var flag = false;
        var text = data.MessageText!;
        if (text[..1] != "/") return flag;
        if (text.StartsWith(Messages.Start))
        {
            var parts = data.MessageText!.Split(" ").ToList();
            if (parts.Count > 1)
            {
                var splitter = parts[1].Split("_").ToList();
                switch (splitter[0])
                {
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
                }
                return flag;
            }
            flag = true;
            await bot.SendMessage(data.ChatId, Messages.Welcome, replyMarkup: mainService.GetMainKeyboard());
        }
        return flag;
    }

    private async Task<bool> CheckKeyboard(UpdateData data)
    {
        var flag = false;
        var keyboardSymbol = "";

        try
        {
            keyboardSymbol = data.MessageText![..3];
        }
        catch (Exception e) { /*ignored*/ }
        
        switch (keyboardSymbol)
        {
            case Messages.PeriodTrackerSymbol:
                await cycleTrackerHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.CartSymbol:
                await cartHandler.HandleSection(data);
                flag = true;
                break;
            case Messages.AboutSymbol:
                flag = true;
                break;
        }
        return flag;
    }

    private async Task<bool> CheckSession(UpdateData data)
    {
        var flag = false;
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return flag;
        if (session.Timestamp.AddMinutes(10) < DateTime.Now)
        {
            sessionService.ClearSession(data.ChatId);
            return flag;
        }
        switch (session.Action)
        {
            case Actions.AwaitingProductActions:
                await cartHandler.AddProductToCart(data, session.CallbackData);
                flag = true;
                break;
        }
        return flag;
    }
}
