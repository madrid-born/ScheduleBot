using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class NotificationHandler(UserSessionService sessionService, MainService services, NotificationService nServices)
{
    #region Handel

    public async Task HandleSection(UpdateData data)
    {
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.KeyboardCreateNotification,     CallBacks.CreateNotification)],
            [new(Messages.KeyboardNotificationManagement, CallBacks.NotificationManagement)]
        ];
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Notification}|{CallBacks.MainSection}|");
        await services.SendMessage(data.ChatId, Messages.LoadNotification, replyMarkup: keyboard);
    }

    public async Task HandleCallBack(UpdateData data)
    {
        var action = data.DataSeparated.ElementAtOrDefault(1);
        var value = data.DataSeparated.ElementAtOrDefault(2);
        switch (action)
        {
            case CallBacks.MainSection:
                switch (value)
                {
                    case CallBacks.CreateNotification:
                        sessionService.SetData(data.ChatId, action: Actions.SetUpNotification, callbackData: SessionCallBacks.AskNotificationName);
                        await services.SendMessage(data.ChatId, Messages.AskNotificationName, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.NotificationManagement:
                        // await LoadNotifications(...);
                        break;
                }
                break;
            case CallBacks.NotificationsHowOften:
                await SetHowOften(data, value!);
                break;
        }
    }
    
    #endregion

    #region CreationOrEdit

    public async Task CreateNotification(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);

        session.SetContext(Context.NotificationName, data.MessageText!);
        await services.SendDatePicker(data.ChatId, DatePickerMethods.NotificationFirstOccurrence, Messages.FirstOccurrence);
    }

    public async Task SetFirstOccurrence(long chatId, DateTime fixedDate, bool isJalali)
    {
        var session = sessionService.GetData(chatId);

        session.SetContext(Context.FirstOccurrence, fixedDate);
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.NotificationOneTime, CallBacks.NotificationOneTime.ToString())],
            [new(Messages.NotificationHour, CallBacks.NotificationHour.ToString())],
            [new(Messages.NotificationDay, CallBacks.NotificationDay.ToString())],
            [new(Messages.NotificationMonth, isJalali ? CallBacks.NotificationMonthJalali.ToString() : CallBacks.NotificationMonthGregorian.ToString())],
        ];
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Notification}|{CallBacks.NotificationsHowOften}|");
        await services.SendMessage(chatId, Messages.ReminderHowOften, replyMarkup: keyboard);
    }

    public async Task SetHowOften(UpdateData data, string callBack)
    {
        var session = sessionService.GetData(data.ChatId);
        var callBackInt = int.Parse(callBack);
        session.SetContext(Context.ReminderUnit, callBackInt);
        var unit = callBackInt switch
        {
            CallBacks.NotificationHour => Messages.NotificationHour,
            CallBacks.NotificationDay => Messages.NotificationDay,
            CallBacks.NotificationMonthJalali => Messages.NotificationMonth,
            CallBacks.NotificationMonthGregorian => Messages.NotificationMonth,
            _ => ""
        };
        
        var numbers = Enumerable.Range(1, 9).ToList();
        var collection = services.LoadCollectionForNormalKeyboard(numbers, width:3);
        var keyboard = services.CreateKeyboard(normalCollection: collection);
        
        if (callBackInt != CallBacks.NotificationOneTime)
        {
            session.SetCallBack(SessionCallBacks.AskNotificationOftenUnit);
            await services.SendMessage(data.ChatId, string.Format(Messages.HowOftenUnit, unit), replyMarkup: keyboard);
        }
        else
        {
            session.SetCallBack(SessionCallBacks.AskReminderMessage);
            await services.SendMessage(data.ChatId, Messages.ReminderMessage);
        }
    }
    
    public async Task SetNotificationOftenUnit(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var unitCount))
        {
            await services.SendMessage(data.ChatId, Messages.InvalidInteger);
            return;
        }
        
        var session = sessionService.GetData(data.ChatId);
        session.SetContext(Context.UnitCount, unitCount);
        session.SetCallBack(SessionCallBacks.AskReminderMessage);
        
        await services.SendMessage(data.ChatId, Messages.ReminderMessage);
    }
    
    public async Task SetNotificationReminderMessage(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);

        session.SetContext(Context.ReminderMessage, data.MessageText!);
        await SaveReminder(data.ChatId);
    }

    private async Task SaveReminder(long chatId)
    {
        var session = sessionService.GetData(chatId);
        
        var notificationName = (string) session.Context[Context.NotificationName];
        var firstOccurrence = (DateTime) session.Context[Context.FirstOccurrence];
        var reminderMessage = (string) session.Context[Context.ReminderMessage];
        var reminderUnit = (int) session.Context[Context.ReminderUnit];
        var unitCount = reminderUnit == 0 ? null : (int?) session.Context[Context.UnitCount];
        
        await nServices.CreateNewReminder(notificationName, firstOccurrence, reminderUnit, unitCount, reminderMessage);
        sessionService.ClearSession(chatId);
    }

    #endregion

    #region NotYetDecided
    
    private async Task LoadNotifications(long chatId, string callBack, int pageNumber = 0)
    {
        var notifications = await nServices.GetNotificationsByTelId(chatId);
        var collection = services.LoadCollectionInPages(notifications, callBack, pageNumber, x => x.Id, x => x.Name!);
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Notification}|");
        await services.SendMessage(chatId, Messages.SelectNotification, replyMarkup: keyboard);
    }

    #endregion
}