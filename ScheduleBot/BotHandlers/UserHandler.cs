using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class UserHandler(MainService services, UserSessionService sessionService, DatabaseService databaseService)
{
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
        var user = await databaseService.GetUserByTelId(chatId);
        
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

            var collection = new List<List<Tuple<string, string>>>
            {
                new() { new(Messages.Yes, $"{CallBacks.AskToRegister}") }
            };
            
            var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Register}|");
            await services.SendMessage(chatId, Messages.NotDefinedUser, replyMarkup: keyboard);
            return false;
        }
        
        if (!user.IsAccepted && !string.IsNullOrEmpty(user.Email))
        {
            await services.SendMessage(chatId, Messages.AdminApprovalPending);
            return false;
        }
        
        return true;
    }

    public async Task AskForName(UpdateData data)
    {
        await databaseService.InsertEmptyUser(data.ChatId, data.Username);
        sessionService.SetData(data.ChatId, Actions.Register, callbackData: SessionCallBacks.AskForName);
        await services.SendMessage(data.ChatId, Messages.EnterYourName, replyMarkup: new ForceReplyMarkup());
    }

    public async Task AskForEmail(UpdateData data)
    {        
        var session = sessionService.GetData(data.ChatId);
        session.SetCallBack(SessionCallBacks.AskForEmail);
        await databaseService.InsertUserName(data.ChatId, data.MessageText);
        await services.SendMessage(data.ChatId, Messages.EnterYourEmail, replyMarkup: new ForceReplyMarkup());
    }

    public async Task RegisterUser(UpdateData data)
    {
        var user = await databaseService.InsertUserEmail(data.ChatId, data.MessageText);
        
        var adminMessage = string.Format(Messages.AdminMessageTemplate, user.Id, user.ChatId, user.Name, user.Email, "[@"+user.Username+"]");
        
        var collection = new List<List<Tuple<string, string>>>
        {
            new()
            {
                new(Messages.Yes, $"{CallBacks.AcceptRegister}|{user.ChatId}"),
                new(Messages.No, $"{CallBacks.RejectRegister}|{user.ChatId}"),
            }
        };
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Register}|");

        
        await services.SendMessage(services.AdminChatId, adminMessage, replyMarkup: keyboard);
        await services.SendMessage(data.ChatId, Messages.RegistrationSuccessful);
        sessionService.ClearSession(data.ChatId);
    }
    
    private async Task AdminApproval(UpdateData data, bool accept)
    {
        var chatId = long.Parse(data.DataSeparated[2]);
        var status = accept ? Messages.Approved : Messages.Rejected ;
        await databaseService.UpdateUserAcceptance(chatId, accept);
        await services.SendMessage(services.AdminChatId, string.Format(Messages.AdminAcceptanceTemplate, chatId ,status));
        await services.SendMessage(chatId, string.Format(Messages.UserAcceptanceTemplate, status));
    }
}