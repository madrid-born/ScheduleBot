using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ScheduleBot.Services;

public class MessageHandler(
    ITelegramBotClient bot,
    DatabaseService db,
    UserService userService,
    IConfiguration configuration)
{
    private readonly long _adminChatId = configuration.GetValue<long>("Telegram:AdminChatId");

    public async Task HandleUpdateAsync(ITelegramBotClient bot1, Update update, CancellationToken ct)
    {
        try
        {
            var updateData = ExtractUpdateDataAsync(update);
            if(!await userService.CheckUserStatusAsync(updateData.ChatId)) return;
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
            data.MessageId = update.CallbackQuery.Message.MessageId;
            data.CallbackData = update.CallbackQuery.Data;
            data.DataSeparated = (update.CallbackQuery.Data ?? "").Split("-").ToList();
            if (update.CallbackQuery.Message.Text == null) return data;
            data.MessageText = update.CallbackQuery.Message.Text;
            data.MessageSeparated = (update.CallbackQuery.Message.Text ?? "").Split('"').ToList();
        }
        else if (update.Message != null)
        {
            data.ChatId = update.Message.Chat.Id;
            data.MessageId = update.Message.MessageId;
            data.MessageText = update.Message.Text;
            data.MessageSeparated = (update.Message.Text ?? "").Split('"').ToList();

            data.Command = update.Message.Text;
            
            if (update.Message.ReplyToMessage == null) return data;
            data.IsReplied = true;
            data.RepliedMessage = update.Message.ReplyToMessage.Text;
            data.ReplyMessageSeparated = (update.Message.ReplyToMessage.Text ?? "").Split('"').ToList();

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
            case "Cart":
                // await HandleCartCallback(data);
                break;
        }
    }

    private async Task HandleMessageAsync(UpdateData data)
    {
        if (data.IsReplied)
        {
            if (data.RepliedMessage == "Please enter your Name")
            {
                // data.ReplyMessageSeparated = ["", data.MessageText ?? ""];
                // await userService.RegisterUserAsync(data.ChatId, name, email, _adminChatId, bot);
                // await bot.SendMessage(data.ChatId, "Please enter your Email", replyMarkup: new ForceReplyMarkup());
                // await bot.SendMessage(data.ChatId, message, parseMode: ParseMode.Markdown, replyMarkup: GetMainKeyboard());
            }
        }
        
        switch (data.MessageText)
        {
            case "ExampleCommand":
                // var carts = await db.GetUserCartsAsync(data.ChatId);
                // await bot.SendMessage(data.ChatId, "Enter the name of the product", replyMarkup: new ForceReplyMarkup());
                // await cartService.ShowCartSelectionAsync(data.ChatId, data.MessageId, "RemoveItem", null, bot);
                break;
        }
    }
}
