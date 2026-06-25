using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class UserHandler(ITelegramBotClient bot, DatabaseService db, MainService services, IConfiguration configuration)
{
    private readonly long _adminChatId = configuration.GetValue<long>("Telegram:AdminChatId");
    
    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case CallBacks.AskToRegister:
                await AskForName(data);
                break;
            case CallBacks.AcceptRegister:
                await AdminApproval(data, true);
                break;
            case CallBacks.RejectRegister:
                await AdminApproval(data, false);
                break;
        }
    }
    
    public async Task<bool> CheckUserStatusAsync(UpdateData data)
    {
        var chatId = data.ChatId;
        var user = await db.GetUserByTelId(chatId);
        
        if (user == null)
        {
            try
            {
                if (data.DataSeparated[0] == CallBacks.Register && data.DataSeparated[1] == CallBacks.AskToRegister)
                {
                    return true;
                }
            }
            catch (Exception e) { /*ignored*/ }
            
            var keyboard = new InlineKeyboardMarkup([[InlineKeyboardButton.WithCallbackData(Messages.Yes, $"{CallBacks.Register}\\{CallBacks.AskToRegister}")]]);
            await bot.SendMessage(chatId, Messages.NotDefinedUser, replyMarkup: keyboard);
            return false;
        }
        
        if (!user.IsAccepted && !string.IsNullOrEmpty(user.Email))
        {
            await bot.SendMessage(chatId, Messages.AdminApprovalPending);
            return false;
        }
        
        return true;
    }

    public async Task AskForName(UpdateData data)
    {
        await db.InsertEmptyUser(data.ChatId, data.Username);
        await bot.SendMessage(data.ChatId, Messages.EnterYourName, replyMarkup: new ForceReplyMarkup());
    }

    public async Task AskForEmail(UpdateData data)
    {
        await db.InsertUserName(data.ChatId, data.MessageText);
        await bot.SendMessage(data.ChatId, Messages.EnterYourEmail, replyMarkup: new ForceReplyMarkup());
    }

    public async Task RegisterUser(UpdateData data)
    {
        var user = await db.InsertUserEmail(data.ChatId, data.MessageText);
        
        var adminMessage = string.Format(Messages.AdminMessageTemplate, user.Id, user.ChatId, user.Name, user.Email, "@"+user.Username);
        var keyboard = new InlineKeyboardMarkup
        ([[
            InlineKeyboardButton.WithCallbackData(Messages.Yes, $"{CallBacks.Register}\\{CallBacks.AcceptRegister}\\{user.ChatId}"),
            InlineKeyboardButton.WithCallbackData(Messages.No, $"{CallBacks.Register}\\{CallBacks.RejectRegister}\\{user.ChatId}"),
        ]]);
        await bot.SendMessage(_adminChatId, adminMessage, replyMarkup: keyboard);
        await bot.SendMessage(data.ChatId, Messages.RegistrationSuccessful);
    }
    
    private async Task AdminApproval(UpdateData data, bool accept)
    {
        var chatId = long.Parse(data.DataSeparated[2]);
        var status = accept ? Messages.Approved : Messages.Rejected ;
        await db.UpdateUserAcceptance(chatId, accept);
        await bot.SendMessage(_adminChatId, string.Format(Messages.AdminAcceptanceTemplate, chatId ,status));
        await bot.SendMessage(chatId, string.Format(Messages.UserAcceptanceTemplate, status),
            replyMarkup: accept ? services.GetMainKeyboard() : null);
    }
}