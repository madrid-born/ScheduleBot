using ScheduleBot.Models;
using ScheduleBot.Services;
using System.Net;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class NotificationHandler(
    UserSessionService sessionService,
    MainService services,
    NotificationService nServices,
    SpotifyHandler spotifyHandler,
    CycleTrackerHandler cycleTrackerHandler)
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
                        await LoadNotifications(data.ChatId);
                        break;
                }
                break;
            case CallBacks.NotificationsHowOften:
                await SetHowOften(data, value!);
                break;
            case CallBacks.NotificationSelect when Guid.TryParse(value, out var notificationId):
                await BeginNotificationEdit(data.ChatId, notificationId);
                break;
            case CallBacks.PreviousPage when value == CallBacks.NotificationSelect &&
                                                      int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var previousPage):
                await LoadNotifications(data.ChatId, previousPage - 1);
                break;
            case CallBacks.NextPage when value == CallBacks.NotificationSelect &&
                                                  int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var nextPage):
                await LoadNotifications(data.ChatId, nextPage + 1);
                break;
            case CallBacks.NotificationEditField:
                await HandleEditField(data.ChatId, value);
                break;
            case CallBacks.NotificationEditType when int.TryParse(value, out var separationType):
                await SaveSeparationType(data.ChatId, separationType);
                break;
            case CallBacks.NotificationToggleActive:
                await ToggleActive(data.ChatId);
                break;
            case CallBacks.NotificationBackToList:
                sessionService.ClearSession(data.ChatId);
                await LoadNotifications(data.ChatId);
                break;
        }
    }
    
    #endregion

    #region CreationOrEdit

    public async Task CreateNotification(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);

        session.SetContext(Context.NotificationName, data.MessageText!);
        await services.SendDatePicker(data.ChatId, method: DatePickerMethods.NotificationFirstOccurrence, message: Messages.FirstOccurrence);
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
        await services.SendMessage(data.ChatId, Messages.ReminderSetSuccessful);
    }

    private async Task SaveReminder(long chatId)
    {
        var session = sessionService.GetData(chatId);
        
        var notificationName = (string) session.Context[Context.NotificationName];
        var firstOccurrence = (DateTime) session.Context[Context.FirstOccurrence];
        var reminderMessage = (string) session.Context[Context.ReminderMessage];
        var reminderUnit = (int) session.Context[Context.ReminderUnit];
        var unitCount = reminderUnit == 0 ? null : (int?) session.Context[Context.UnitCount];
        
        var notificationId = await nServices.CreateNewReminder(chatId, notificationName, firstOccurrence, reminderUnit, unitCount, reminderMessage);
        var futureNotifications = await nServices.GetFutureNotificationsByNotificationId(notificationId);
        var botSession = sessionService.GetData(0);
        var notifications = (List<ToBeSentNotification>) botSession.Context[Context.BotNotifications];
        notifications.AddRange(futureNotifications);
        botSession.SetContext(Context.BotNotifications, notifications);
        
        sessionService.ClearSession(chatId);
    }

    #endregion

    #region Management
    
    private async Task LoadNotifications(long chatId, int pageNumber = 0)
    {
        var notifications = await nServices.GetManageableNotificationsByTelId(chatId);
        if (notifications.Count == 0)
        {
            await services.SendMessage(chatId, Messages.NoNotifications);
            return;
        }

        pageNumber = Math.Clamp(pageNumber, 0, (notifications.Count - 1) / 9);
        var collection = services.LoadCollectionInPages(notifications, CallBacks.NotificationSelect, pageNumber,
            x => x.Id, x => x.Name.Length > 40 ? $"{x.Name[..39]}…" : x.Name);
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Notification}|");
        await services.SendMessage(chatId, Messages.SelectNotification, replyMarkup: keyboard);
    }

    private async Task BeginNotificationEdit(long chatId, Guid notificationId)
    {
        var details = await nServices.GetNotificationForManagement(chatId, notificationId);
        if (details == null)
        {
            await services.SendMessage(chatId, Messages.NotificationUpdateFailed);
            return;
        }

        var session = sessionService.SetData(chatId, Actions.EditNotification);
        session.SetContext(Context.NotificationId, notificationId);
        await ShowNotificationEditor(chatId, details);
    }

    private async Task ShowNotificationEditor(long chatId, NotificationManagementDetails? details = null)
    {
        var session = sessionService.GetData(chatId);
        if (!session.Context.TryGetValue(Context.NotificationId, out var idValue) || idValue is not Guid notificationId)
        {
            await services.SendMessage(chatId, Messages.NotificationUpdateFailed);
            return;
        }

        details ??= await nServices.GetNotificationForManagement(chatId, notificationId);
        if (details == null)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.NotificationUpdateFailed);
            return;
        }

        var message = string.Format(Messages.NotificationEditor,
            EncodePreview(details.Name),
            details.IsActive ? Messages.NotificationActive : Messages.NotificationInactive,
            EncodePreview(details.Message),
            details.NextTime?.ToString("yyyy-MM-dd HH:mm") ?? Messages.NotificationNoNextOccurrence,
            EncodePreview(details.NextMessage ?? Messages.NotificationUsesDefaultMessage),
            FormatSeparation(details.Type, details.SeparationValue));

        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.NotificationEditName, $"{CallBacks.NotificationEditField}|Name")],
            [new(Messages.NotificationEditMessage, $"{CallBacks.NotificationEditField}|Message")],
            [new(Messages.NotificationEditNextTime, $"{CallBacks.NotificationEditField}|NextTime")],
            [new(Messages.NotificationEditSeparationType, $"{CallBacks.NotificationEditField}|SeparationType")]
        ];
        if (details.NextTime != null)
            collection.Insert(3, [new(Messages.NotificationEditNextMessage, $"{CallBacks.NotificationEditField}|NextMessage")]);
        if (details.Type != CallBacks.NotificationOneTime)
            collection.Add([new(Messages.NotificationEditSeparationValue, $"{CallBacks.NotificationEditField}|SeparationValue")]);
        collection.Add([new(details.IsActive ? Messages.NotificationDeactivate : Messages.NotificationActivate,
            CallBacks.NotificationToggleActive)]);
        collection.Add([new(Messages.NotificationBackToList, CallBacks.NotificationBackToList)]);

        await services.SendMessage(chatId, message,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Notification}|"),
            parseMode: ParseMode.Html);
    }

    private async Task HandleEditField(long chatId, string? field)
    {
        var session = sessionService.GetOrSetData(chatId);
        if (session.Action != Actions.EditNotification) return;

        switch (field)
        {
            case "Name":
                session.SetCallBack(SessionCallBacks.AskEditedNotificationName);
                await services.SendMessage(chatId, Messages.AskEditedNotificationName, new ForceReplyMarkup());
                break;
            case "Message":
                session.SetCallBack(SessionCallBacks.AskEditedNotificationMessage);
                await services.SendMessage(chatId, Messages.AskEditedNotificationMessage, new ForceReplyMarkup());
                break;
            case "NextTime":
                var details = await GetCurrentDetails(chatId);
                if (details != null)
                    await services.SendDatePicker(chatId, method: DatePickerMethods.NotificationNextOccurrence,
                        message: Messages.NotificationEditNextTime,
                        passedDate: details.NextTime ?? services.GetIranDateTime().AddHours(1), isJalali: false);
                break;
            case "NextMessage":
                session.SetCallBack(SessionCallBacks.AskEditedNextMessage);
                await services.SendMessage(chatId, Messages.AskEditedNextMessage, new ForceReplyMarkup());
                break;
            case "SeparationType":
                await ShowSeparationTypeSelector(chatId);
                break;
            case "SeparationValue":
                session.SetCallBack(SessionCallBacks.AskEditedSeparationValue);
                await services.SendMessage(chatId, Messages.AskEditedSeparationValue, new ForceReplyMarkup());
                break;
        }
    }

    private async Task ShowSeparationTypeSelector(long chatId)
    {
        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.NotificationOneTime, CallBacks.NotificationOneTime.ToString())],
            [new(Messages.NotificationHour, CallBacks.NotificationHour.ToString())],
            [new(Messages.NotificationDay, CallBacks.NotificationDay.ToString())],
            [new($"{Messages.NotificationMonth} (Gregorian)", CallBacks.NotificationMonthGregorian.ToString())],
            [new($"{Messages.NotificationMonth} (Jalali)", CallBacks.NotificationMonthJalali.ToString())]
        ];
        await services.SendMessage(chatId, Messages.SelectNotificationSeparationType,
            services.CreateKeyboard(inlineCollection: collection,
                callBackStart: $"{CallBacks.Notification}|{CallBacks.NotificationEditType}|"));
    }

    public async Task EditNotificationName(UpdateData data) =>
        await SaveTextEdit(data, nServices.UpdateNotificationName);

    public async Task EditNotificationMessage(UpdateData data) =>
        await SaveTextEdit(data, nServices.UpdateNotificationMessage);

    public async Task EditNextMessage(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var notificationId = (Guid)session.Context[Context.NotificationId];
        var message = string.Equals(data.MessageText?.Trim(), Messages.Skip, StringComparison.OrdinalIgnoreCase)
            ? null
            : data.MessageText?.Trim();
        var updated = message == null || !string.IsNullOrWhiteSpace(message)
            ? await nServices.UpdateNextMessage(data.ChatId, notificationId, message)
            : false;
        await FinishEdit(data.ChatId, updated);
    }

    public async Task EditSeparationValue(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var notificationId = (Guid)session.Context[Context.NotificationId];
        var updated = int.TryParse(data.MessageText, out var separationValue) && separationValue > 0 &&
                      await nServices.UpdateSeparationValue(data.ChatId, notificationId, separationValue);
        await FinishEdit(data.ChatId, updated);
    }

    public async Task SetEditedNextOccurrence(long chatId, DateTime nextTime)
    {
        var session = sessionService.GetData(chatId);
        var notificationId = (Guid)session.Context[Context.NotificationId];
        var updated = nextTime > services.GetIranDateTime() &&
                      await nServices.UpdateNextOccurrence(chatId, notificationId, nextTime);
        if (!updated)
        {
            session.ClearDatePicker();
            await services.SendMessage(chatId, Messages.NotificationUpdateFailed);
            await ShowNotificationEditor(chatId);
            return;
        }

        await FinishEdit(chatId, updated);
    }

    private async Task SaveTextEdit(UpdateData data, Func<long, Guid, string, Task<bool>> update)
    {
        var session = sessionService.GetData(data.ChatId);
        var text = data.MessageText?.Trim();
        var notificationId = (Guid)session.Context[Context.NotificationId];
        var updated = !string.IsNullOrWhiteSpace(text) && await update(data.ChatId, notificationId, text);
        await FinishEdit(data.ChatId, updated);
    }

    private async Task SaveSeparationType(long chatId, int separationType)
    {
        var session = sessionService.GetOrSetData(chatId);
        if (session.Action != Actions.EditNotification) return;
        var notificationId = (Guid)session.Context[Context.NotificationId];
        await FinishEdit(chatId, await nServices.UpdateSeparationType(chatId, notificationId, separationType));
    }

    private async Task ToggleActive(long chatId)
    {
        var session = sessionService.GetOrSetData(chatId);
        if (session.Action != Actions.EditNotification) return;
        var notificationId = (Guid)session.Context[Context.NotificationId];
        var updated = await nServices.ToggleNotificationActive(chatId, notificationId);
        if (!updated)
        {
            await services.SendMessage(chatId, Messages.NotificationActivationFailed);
            await ShowNotificationEditor(chatId);
            return;
        }

        await FinishEdit(chatId, true);
    }

    private async Task FinishEdit(long chatId, bool updated)
    {
        await services.SendMessage(chatId, updated ? Messages.NotificationUpdated : Messages.NotificationUpdateFailed);
        if (!updated) return;

        var session = sessionService.GetData(chatId);
        session.SetCallBack(string.Empty);
        session.ClearDatePicker();
        await RefreshUpcomingNotifications();
        await ShowNotificationEditor(chatId);
    }

    private async Task<NotificationManagementDetails?> GetCurrentDetails(long chatId)
    {
        var session = sessionService.GetData(chatId);
        return session.Context.TryGetValue(Context.NotificationId, out var idValue) && idValue is Guid notificationId
            ? await nServices.GetNotificationForManagement(chatId, notificationId)
            : null;
    }

    private async Task RefreshUpcomingNotifications()
    {
        var notifications = await nServices.GetNotificationsForNextHour(services.GetIranDateTime());
        sessionService.GetOrSetData(0).SetContext(Context.BotNotifications, notifications);
    }

    private static string FormatSeparation(int type, int? value) => type switch
    {
        CallBacks.NotificationOneTime => Messages.NotificationOneTime,
        CallBacks.NotificationHour => $"Every {value} hour(s)",
        CallBacks.NotificationDay => $"Every {value} day(s)",
        CallBacks.NotificationMonthGregorian => $"Every {value} Gregorian month(s)",
        CallBacks.NotificationMonthJalali => $"Every {value} Jalali month(s)",
        _ => "Unknown"
    };

    private static string EncodePreview(string value)
    {
        const int maxLength = 700;
        var preview = value.Length > maxLength ? $"{value[..maxLength]}…" : value;
        return WebUtility.HtmlEncode(preview);
    }

    #endregion

    #region CheckForNotifications
    
    public async Task CheckAndSendNotifications(bool install = false)
    {
        try
        {
            var now = services.GetIranDateTime();
            var session = sessionService.GetData(0);
            List<ToBeSentNotification> notifications = null!;
            try
            {
                notifications = (List<ToBeSentNotification>) session.Context[Context.BotNotifications];
            }
            catch (Exception)
            {
                install = true;
            }

            
            if (install || now.TimeOfDay.Minutes == 0)
            {
                var notificationsForNextHour = await nServices.GetNotificationsForNextHour(now);
                session.SetContext(Context.BotNotifications, notificationsForNextHour);
            }
            else
            {
                foreach (var notification in notifications.Where(notification => notification.Time <= now).ToList())
                {
                    notifications.Remove(notification);
                    await nServices.RenewFutureNotifications(notification.FutureNotificationId);
                    try
                    {
                        switch (notification.SpecialBehavior)
                        {
                            case CallBacks.SpecialAdminCheckSpotify:
                                await spotifyHandler.CheckForNewDeleted(notification.ChatId);
                                break;
                            case CallBacks.SpecialPeriodTracker:
                                await cycleTrackerHandler.SendPeriodTrackerNotifications(notification.NotificationId);
                                break;
                            default:
                                await services.SendMessage(notification.ChatId, notification.Message);
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        if (notification.SpecialBehavior == 0 && !string.IsNullOrWhiteSpace(notification.Message))
                            await services.SendMessage(notification.ChatId, notification.Message);
                    }
                }
                session.SetContext(Context.BotNotifications, notifications);
            }
            
        }
        catch (Exception e)
        {
            if (DateTime.Now.Minute == 0)
            {
                await services.SendMessage(services.AdminChatId, "Notification handler is down\n\n" + e.Message);
            }
        }
    }

    #endregion
}
