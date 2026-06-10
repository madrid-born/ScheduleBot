using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class MessageHandler(
    ITelegramBotClient bot,
    DatabaseService db,
    UserService userService,
    CycleTrackerService cycleTracker,
    IConfiguration configuration)
{
    private readonly long _adminChatId = configuration.GetValue<long>("Telegram:AdminChatId");

    public async Task HandleUpdateAsync(ITelegramBotClient bot1, Update update, CancellationToken ct)
    {
        try
        {
            var updateData = ExtractUpdateDataAsync(update);
            if(!await userService.CheckUserStatusAsync(updateData)) return;
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
            Console.WriteLine($"Error: {ex.Message}");
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
        switch (data.DataSeparated[0])
        {
            case CallBacks.Register:
                await userService.HandleCallBack(data);
                break;
            case CallBacks.Cycle:
                await cycleTracker.HandleCallBack(data);
                break;
        }
    }

    private async Task HandleMessageAsync(UpdateData data)
    {
        var flag = true;
        if (data.IsReplied)
        {
            flag = false;
            switch (data.ReplyMessageSeparated[0])
            {
                case Messages.EnterYourName: await userService.AskForEmail(data); break;
                case Messages.EnterYourEmail: await userService.RegisterUser(data); break;
                case Messages.SetupTracker: await cycleTracker.SaveLastPeriodStart(data); break;
                case Messages.AskForCycleLength: await cycleTracker.SaveCycleLength(data); break;
                case Messages.AskForPeriodLength: await cycleTracker.SavePeriodLength(data); break;
                default: flag = true; break;
            }
        }
        switch (data.MessageText)
        {
            case "/start":
                await bot.SendMessage(data.ChatId, Messages.Welcome, replyMarkup: GetMainKeyboard());
                break;
            case "/SetupPeriod":
                await cycleTracker.AskForLastPeriodStart(data);
                break;
            case "/EditPeriod":
                await cycleTracker.ShowEditMenu(data.ChatId);
                break;
            default:
                if (flag) await bot.SendMessage(data.ChatId, Messages.NotFound, replyMarkup: GetMainKeyboard());
                break;
        }
    }
    
    public static ReplyKeyboardMarkup GetMainKeyboard()
    {
        return new ReplyKeyboardMarkup
        ([
            [new KeyboardButton("🌸 Period Tracker"), new KeyboardButton("ℹ️ About")],
        ])
        { ResizeKeyboard = true };
    }
}
