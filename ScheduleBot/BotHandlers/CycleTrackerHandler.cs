using System.Globalization;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class CycleTrackerHandler(ITelegramBotClient bot, IServiceProvider serviceProvider,
    MainService services, CycleTrackerService ctServices, ILogger<CycleTrackerHandler> logger)
{
    private readonly TimeSpan _notificationHour = new(12, 30, 0);
    
    public async Task HandleSection(UpdateData data)
    {
        switch (data.MessageText![3..])
        {
            case Messages.PeriodTracker:
                await StartSection(data);
                break;
            case Messages.KeyboardSetup:
                await AskForLastPeriodStart(data);
                break;
            case Messages.KeyboardReportStart:
                await AskReportStart(data);
                break;
            case Messages.KeyboardReportEnd:
                await AskReportEnd(data);
                break;
            case Messages.KeyboardEdit:
                await EditCheck(data);
                break;
            case Messages.KeyboardCurrentStatus:
                await SendCurrentStatusList(data);
                break;
            case Messages.KeyboardAddToCycle:
                await AddToCycle(data);
                break;
            case Messages.KeyboardJoinToCycle:
                await JoinToCyclePressed(data);
                break;
        }
    }
    
    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case CallBacks.ReportStart:
                await ReportStart(data);
                break;
            case CallBacks.ReportEnd:
                await ReportEnd(data);
                break;
            case CallBacks.SetNotifyMode:
                await SetNotifyMode(data);
                break;
            case CallBacks.CurrentStatus:
                await SendCurrentStatus(data);
                break;
            case CallBacks.EditSection:
                //todo
                await EditCycle(data);
                break;
            case CallBacks.EditNotify:
                await ShowNotifyModeMenu(data.ChatId, Guid.Parse(data.DataSeparated[2]));
                break;
            case CallBacks.RemoveFollowing:
                await RemoveFollowing(data);
                break;
            case CallBacks.RemoveFollower:
                await RemoveFollower(data);
                break;
        }
    }
    
    private async Task StartSection(UpdateData data)
    {
        var collection = new List<List<string>>
        {
            new() { Messages.KeyboardSetup, Messages.KeyboardEdit },
            new() { Messages.KeyboardReportStart, Messages.KeyboardReportEnd },
            new() { Messages.KeyboardAddToCycle, Messages.KeyboardJoinToCycle },
            new() { Messages.KeyboardCurrentStatus },
        };
        
        var keyboard = services.CreateKeyboard(collection, symbol: Messages.PeriodTrackerSymbol, resizeKeyboard: true);

        await services.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: keyboard);
    }
    
    private async Task AskReportStart(UpdateData data)
    {
        await services.ApproveKeyboardInline(data.ChatId, Messages.DidItStart, $"{CallBacks.Cycle}\\{CallBacks.ReportStart}\\");
    }
    
    private async Task AskReportEnd(UpdateData data)
    {
        await services.ApproveKeyboardInline(data.ChatId, Messages.DidItEnd, $"{CallBacks.Cycle}\\{CallBacks.ReportEnd}\\");
    }
    
    private async Task ReportStart(UpdateData data)
    {        
        var result = data.DataSeparated[2];
        switch (result)
        {
            case CallBacks.Yes:
                await services.SendMessage(data.ChatId, Messages.SavedData, true);
                await ctServices.SetNewStartByTelId(data.ChatId, DateTime.Now);
                await StartNotify(data.ChatId);
                break;
            case CallBacks.No:
                await ctServices.SaveLastCycleHistory(data.ChatId);
                await AskForLastPeriodStart(data);
                break;
        }
    }
    
    private async Task ReportEnd(UpdateData data)
    {        
        var result = data.DataSeparated[2];
        switch (result)
        {
            case CallBacks.Yes:
                await ctServices.SetNewEndByTelId(data.ChatId);
                await services.SendMessage(data.ChatId, Messages.SavedData, true);
                await EndNotify(data.ChatId);
                break;
            case CallBacks.No:
                await services.SendMessage(data.ChatId, Messages.Welcome, true);
                break;
        }
    }

    private async Task StartNotify(long chatId)
    {
        var users = await ctServices.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await services.SendMessage(user.ChatId, string.Format(Messages.NotifyStart, $"{user.Name}(@{user.Username})"), true);
        }
    }
    
    private async Task EndNotify(long chatId)
    {
        var users = await ctServices.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await services.SendMessage(user.ChatId, string.Format(Messages.NotifyEnd, $"{user.Name}(@{user.Username})"), true);
        }
    }

    private async Task AskForLastPeriodStart(UpdateData data)
    {
        var existing = await ctServices.GetCycleByTelId(data.ChatId);
        if (existing != null)
        {
            await services.SendMessage(data.ChatId, Messages.AvailableCycle, true);
            return;
        }
        
        await services.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SaveLastPeriodStart(UpdateData data)
    {
        var date = DateValidation(data.MessageText!);
        if (date == null)
        {
            await services.SendMessage(data.ChatId, Messages.InvalidDate);
            await services.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
            return;
        }

        if (await ctServices.Seva(data.ChatId, (DateTime)date))
        {
            // todo
        }
        else await services.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
    }

    private static DateTime? DateValidation(string dataMessageText)
    {
        DateTime? date = null;
        try
        {
            var firstDigit = dataMessageText[..1];
            switch (firstDigit)
            {
                case "1":
                    date = ConvertJalaliToGregorian(dataMessageText);
                    break;
                case "2":
                    if (DateTime.TryParse(dataMessageText, out var gregorianDate)) date = gregorianDate;
                    break;
            }
        }
        catch (Exception e) { /*ignored*/ }

        return date;
    }

    private static DateTime ConvertJalaliToGregorian(string date)
    {
        var pc = new PersianCalendar();
        var year = int.Parse(date.Substring(0, 4));
        var month = int.Parse(date.Substring(5, 2));
        var day = int.Parse(date.Substring(8, 2));
        return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
    }
    
    public static string ConvertGregorianToJalali(DateTime date)
    {
        var pc = new PersianCalendar();

        var year = pc.GetYear(date);
        var month = pc.GetMonth(date);
        var day = pc.GetDayOfMonth(date);

        return $"{year:D4}/{month:D2}/{day:D2}";
    }

    public async Task SaveCycleLength(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var length))
        {
            await services.SendMessage(data.ChatId, Messages.InvalidInteger);
            await services.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        if (await ctServices.Seva2(data.ChatId, length))
        {
            // todo
        }
        else await services.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SavePeriodLength(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var length))
        {
            await services.SendMessage(data.ChatId, Messages.InvalidInteger);
            await services.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        if (await ctServices.Seva3(data.ChatId, length))
        {
            // todo
        }
        else await ShowNotifyModeMenu(data.ChatId);
    }

    private async Task ShowNotifyModeMenu(long chatId, Guid cycleId = default)
    {
        var collection = new List<List<Tuple<string, string>>>();
        collection.AddRange(Messages.NotifyModes.Select((notifyMode, index) =>
            (List<Tuple<string, string>>)[new(notifyMode, $"{index}\\{cycleId}")]));

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.SetNotifyMode}\\");
        await services.SendMessage(chatId, Messages.AskForNotifyMode, replyMarkup: keyboard);
    }

    private async Task SetNotifyMode(UpdateData data)
    {
        var chatId = data.ChatId;
        var mode = int.Parse(data.DataSeparated[2]);
        var cycleId = Guid.Parse(data.DataSeparated[3]);
        var ownerName = await ctServices.SetNotify(chatId, mode, cycleId);
        var message = string.Format(Messages.SetNotifyComplete, Messages.NotifyModes[mode]);
        if (ownerName != null) message = string.Format(Messages.SetNotifyCompleteGuest, ownerName) + message;
        else
        {
            await services.SendMessage(chatId, Messages.SetupComplete);
            await services.SendMessage(chatId, Messages.EditPeriodReminder);
        }
        await services.SendMessage(chatId, message, true);
    }
    
    private async Task EditCheck(UpdateData data, bool commited = false)
    {
        var (cycleLength, periodLength, lastPeriodStart, avgCycleLength, avgPeriodLength, followers) =
            await ctServices.LoadCycleDetail(data.ChatId);
        var message = string.Format(Messages.CurrentData, lastPeriodStart, cycleLength, periodLength, avgCycleLength, avgPeriodLength) +
                      string.Format(Messages.Followers, followers);
        await services.SendMessage(data.ChatId, message, addMainKeyboard: true);
        
        if (!commited)
        {
            var collection = new List<List<Tuple<string, string>>>
            {
                new() {new(Messages.EditPeriodLength, CallBacks.EditPeriodLength)},
                new() {new(Messages.EditCycleLength,  CallBacks.EditCycleLength)},
                new() {new(Messages.EditFollowers,    CallBacks.EditFollowers)},
                new() {new(Messages.EditLastPeriod,   CallBacks.EditLastPeriod)},
                new() {new(Messages.EditNotify,       CallBacks.EditNotify)},
            };
            var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\");

            await services.SendMessage(data.ChatId, Messages.EditCheck, replyMarkup: keyboard);
        }
    }
    
    private async Task EditCycle(UpdateData data)
    {
        switch (data.DataSeparated[2])
        {
            case CallBacks.EditLastPeriod:
                await AskForLastPeriodStart(data);
                break;
            case CallBacks.EditPeriodLength:
                await services.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditCycleLength:
                await services.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditNotify:
                await LoadCycleList(data.ChatId, CallBacks.EditNotify);
                break;
            case CallBacks.EditFollowers:
                await RemoveFollowersList(data);
                break;
            case CallBacks.EditFollowing:
                await RemoveFollowingList(data);
                break;
        }

        await EditCheck(data, true);
    }

    private async Task AddToCycle(UpdateData data)
    {
        var result = (await ctServices.GetCycleByTelId(data.ChatId))!.Id;
        await services.SendMessage(data.ChatId, string.Format(Messages.ShareCycleId, result), true);
    }
    
    private async Task JoinToCyclePressed(UpdateData data)
    {
        await services.SendMessage(data.ChatId, Messages.AskForCycleId, replyMarkup: new ForceReplyMarkup());
    }
    
    public async Task JoinToCycleById(UpdateData data)
    {
        var idAvailable = Guid.TryParse(data.MessageText, out var cycleId);
        if (idAvailable && await ctServices.GetCycleByCycleId(cycleId) != null)
        {
            await ShowNotifyModeMenu(data.ChatId, cycleId);
        }
        else
        {
            await services.SendMessage(data.ChatId, Messages.CycleIdIsWrong, true);
        }
    }
    
    private async Task RemoveFollowingList(UpdateData data)
    {
        await LoadCycleList(data.ChatId, CallBacks.RemoveFollowing);
    }

    private async Task RemoveFollowersList(UpdateData data)
    {
        var collection = new List<List<Tuple<string, string>>>();
        collection.AddRange((await ctServices.GetFollowersByChatId(data.ChatId)).Select(user =>
            (List<Tuple<string, string>>)[new(user.Name!, user.Id.ToString())]));

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.RemoveFollower}\\");
        await services.SendMessage(data.ChatId, Messages.SelectUser, replyMarkup: keyboard);
    }
    
    private async Task RemoveFollower(UpdateData data)
    {
        var cycleId = (await ctServices.GetCycleByTelId(data.ChatId))!.Id;
        var owner = await ctServices.GetUserByTelId(data.ChatId);
        var receiver = await ctServices.GetUserById(Guid.Parse(data.DataSeparated[2]));
        await ctServices.RemoveReceiverFromCycle(cycleId, receiver!.Id);
        
        await services.SendMessage(receiver.ChatId, string.Format(Messages.RemoveFollowerForReceiver, owner!.Name), true);
        await services.SendMessage(owner!.ChatId, string.Format(Messages.RemoveFollowerForOwner, receiver.Name), true);
    }
    
    private async Task RemoveFollowing(UpdateData data)
    {
        var cycleId = Guid.Parse(data.DataSeparated[2]);
        var owner = await ctServices.GetCycleOwnerByCycleId(cycleId);
        var receiver = await ctServices.GetUserByTelId(data.ChatId);
        await ctServices.RemoveReceiverFromCycle(cycleId, receiver!.Id);
        
        await services.SendMessage(receiver.ChatId, string.Format(Messages.RemoveFollowingForReceiver, owner!.Name), true);
        await services.SendMessage(owner!.ChatId, string.Format(Messages.RemoveFollowingForOwner, receiver.Name), true);
    }
    
    private async Task SendCurrentStatusList(UpdateData data)
    {
        await LoadCycleList(data.ChatId, CallBacks.CurrentStatus);
    }
    
    private async Task SendCurrentStatus(UpdateData data)
    {
        var cycleId = Guid.Parse(data.DataSeparated[2]);
        var owner = await ctServices.GetCycleOwnerByCycleId(cycleId);
        var receiver = await ctServices.GetUserByTelId(data.ChatId);
        var message = await ctServices.CreateStatusMessage(cycleId);
        var date = $"{DateTime.Now:MM/dd/yyyy} - {ConvertGregorianToJalali(DateTime.Now)}";
        await services.SendMessage(receiver!.ChatId, string.Format(Messages.StatusForReceiver, date, owner!.Name, message), true);
    }
    
    private async Task LoadCycleList(long chatId, string callBack)
    {
        var collection = new List<List<Tuple<string, string>>>();
        collection.AddRange((await ctServices.GetFollowingByChatId(chatId)).Select(user =>
            (List<Tuple<string, string>>)[new(user.UserName, user.CycleId.ToString())]));

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{callBack}\\");
        await services.SendMessage(chatId, Messages.SelectCycle, replyMarkup: keyboard);
    }
    
    public async Task CheckAndSendNotifications()
    {
        var now = DateTime.UtcNow;
        if (now.TimeOfDay.Hours != _notificationHour.Hours || now.TimeOfDay.Minutes != _notificationHour.Minutes) return;
        var allCycleNotifies = await ctServices.GetAllCycleNotifies();
        
        foreach (var cycleNotify in allCycleNotifies)
        {
            var shouldNotify = ShouldNotifyToday(cycleNotify.cycle, cycleNotify.notify.NotifyMode);
            if (!shouldNotify) continue;
            var date = $"{DateTime.Now:MM/dd/yyyy} - {ConvertGregorianToJalali(DateTime.Now)}";
            var message = await ctServices.CreateStatusMessage(cycleNotify.cycle.Id);
            await services.SendMessage(cycleNotify.receiver.ChatId, string.Format(Messages.StatusForReceiver, date, cycleNotify.owner.Name, message), true);
        }
    }

    private bool ShouldNotifyToday(CycleDetail cycle, int mode)
    {
        var today = DateTime.Now;
        var daysSinceLastStart = (today - cycle.LastStart!.Value).Days;
        var daysUntilNext = cycle.CycleLength - daysSinceLastStart;
        var isInPeriod = today >= cycle.LastStart.Value && today <= cycle.LastEnd!.Value;
        var daysBeforePeriod = daysUntilNext <= 3;
        
        return mode switch
        {
            1 => true,
            2 => today.DayOfWeek == DayOfWeek.Monday,
            4 => daysBeforePeriod || isInPeriod,
            _ => false
        };
    }
}