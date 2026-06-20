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
    CycleTrackerHandler cycleTracker,
    IConfiguration configuration)
{
    private readonly long _adminChatId = configuration.GetValue<long>("Telegram:AdminChatId");

    public async Task HandleUpdateAsync(ITelegramBotClient bot1, Update update, CancellationToken ct)
    {
        long chatId = 0;
        try
        {
            var updateData = ExtractUpdateDataAsync(update);
            chatId = updateData.ChatId;
            if(!await userHandler.CheckUserStatusAsync(updateData)) return;
            if (updateData.IsCallback && !string.IsNullOrEmpty(updateData.CallbackData))
            {
                await HandleCallbackAsync(updateData);
            }
            else
            {
                await HandleMessageAsync(updateData);
            }
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, Messages.NotFound, replyMarkup: GetMainKeyboard());
        }
    }

    private static UpdateData ExtractUpdateDataAsync(Update update)
    {
        var data = new UpdateData();
        
        if (update.CallbackQuery != null)
        {
            data.IsCallback = true;
            data.ChatId = update.CallbackQuery.Message!.Chat.Id;
            data.Username = update.CallbackQuery.Message!.Chat.Username;
            data.MessageId = update.CallbackQuery.Message.MessageId;
            data.CallbackData = update.CallbackQuery.Data;
            data.DataSeparated = (update.CallbackQuery.Data ?? "").Split("\\").ToList();
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
        await bot.DeleteMessage(data.ChatId, data.MessageId);

        switch (data.DataSeparated[0])
        {
            case CallBacks.Register:
                await userHandler.HandleCallBack(data);
                break;
            case CallBacks.Cycle:
                await cycleTracker.HandleCallBack(data);
                break;
        }
    }

    private async Task HandleMessageAsync(UpdateData data)
    {
        var checkReplied = await CheckReplied(data);
        var checkCommand = await CheckCommand(data);
        var checkKeyboard = await CheckKeyboard(data);

        if (!checkReplied && !checkCommand && !checkKeyboard)
        {
            await bot.SendMessage(data.ChatId, Messages.NotFound, replyMarkup: GetMainKeyboard());
        }
    }

    private async Task<bool> CheckReplied(UpdateData data)
    {
        var flag = false;
        if (!data.IsReplied) return flag;
        switch (data.RepliedMessage)
        {
            case Messages.EnterYourName:
                await userHandler.AskForEmail(data);
                flag = true;
                break;
            case Messages.EnterYourEmail:
                await userHandler.RegisterUser(data); 
                flag = true;
                break;
            case Messages.SetupTracker:
                await cycleTracker.SaveLastPeriodStart(data);
                flag = true;
                break;
            case Messages.AskForCycleLength:
                await cycleTracker.SaveCycleLength(data); 
                flag = true;
                break;
            case Messages.AskForPeriodLength:
                await cycleTracker.SavePeriodLength(data);
                flag = true;
                break;
            case Messages.AskForCycleId:
                await cycleTracker.JoinToCycleById(data);
                flag = true;
                break;
        }
        return flag;
    }

    private async Task<bool> CheckCommand(UpdateData data)
    {
        var flag = false;
        if (data.MessageText![..1] != "/") return flag;
        switch (data.MessageText)
        {
            case Messages.Start:
                await bot.SendMessage(data.ChatId, Messages.Welcome, replyMarkup: GetMainKeyboard());
                flag = true;
                break;
            // case Messages.SetupPeriod:
            //     await cycleTracker.AskForLastPeriodStart(data);
            //     flag = true;
            //     break;
            // case Messages.EditPeriod:
            //     await cycleTracker.ShowEditMenu(data.ChatId);
            //     flag = true;
            //     break;
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
                await cycleTracker.HandleSection(data);
                flag = true;
                break;
        }
        return flag;
    }

    public static ReplyKeyboardMarkup GetMainKeyboard()
    {
        return new ReplyKeyboardMarkup
            ([
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.PeriodTracker), new KeyboardButton(Messages.About)],
            ])
            { ResizeKeyboard = true };
    }

    public async Task SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null, bool addMainKeyboard = true)
    {
        await bot.SendMessage(chatId, message, replyMarkup: addMainKeyboard ? GetMainKeyboard() : replyMarkup);
    }
    
    public static ReplyMarkup CreateKeyboard(IEnumerable<IEnumerable<IEnumerable<string>>> collection, bool inline = false,
        string symbol = "", string callBackStart = "", bool resizeKeyboard = true)
    {
        if (inline)
        {
            var keyboard = collection
                .Select(row => row
                    .Select(text =>
                    {
                        var enumerable = text.ToList();
                        return InlineKeyboardButton.WithCallbackData(symbol + enumerable[0], callBackStart + enumerable[1]);
                    })
                    .ToArray())
                .ToArray();

            return new InlineKeyboardMarkup(keyboard);
        }
        else
        {
            var keyboard = collection
                .Select(row => row
                    .Select(text =>
                    {
                        var enumerable = text.ToList();
                        return new KeyboardButton(symbol + enumerable[0]);

                    })
                    .ToArray())
                .ToArray();

            return new ReplyKeyboardMarkup(keyboard){ResizeKeyboard = resizeKeyboard};
        }
    }
    
    public async Task ApproveKeyboardInline(long chatId, string message, string callBackStart)
    {
        var collection = new List<List<List<string>>>
        {
            new() { new(){Messages.Yes, CallBacks.Yes} },
            new() { new(){Messages.No,  CallBacks.No}  },
        };
        
        var keyboard = CreateKeyboard(collection, inline:true, callBackStart: callBackStart);

        await SendMessage(chatId, message, keyboard);
    }

}
