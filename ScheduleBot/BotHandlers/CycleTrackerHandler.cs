using System.Globalization;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class CycleTrackerHandler(ITelegramBotClient bot, IServiceProvider serviceProvider,
    MainService services, CycleTrackerService ctServices, ILogger<CycleTrackerHandler> logger)
{
    
    #region Handel

    public async Task HandleSection(UpdateData data)
    {
        switch (data.MessageText![3..])
        {
            case Messages.PeriodTracker:
                await StartSection(data);
                break;
        }
    }
    
    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case CallBacks.StartSection:
                await Start(data);
                break;
            case CallBacks.SetNotifyMode:
                await SetNotifyMode(data);
                break;
            case CallBacks.EditSection:
                await EditCycle(data);
                break;
            case CallBacks.CurrentStatus:
                await SendCurrentStatus(data);
                break;
            case CallBacks.ReportStart:
                await Report(data, true);
                break;
            case CallBacks.ReportEnd:
                await Report(data, false);
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
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.Edit,        CallBacks.Edit),        new(Messages.CurrentStatus, CallBacks.CurrentStatus)],
            [new(Messages.ReportStart, CallBacks.ReportStart), new(Messages.ReportEnd, CallBacks.ReportEnd)],
            [new(Messages.AddToCycle,  CallBacks.AddToCycle)],
            [new(Messages.JoinToCycle, CallBacks.JoinToCycle)]
        ];
        
        if (await ctServices.GetCycleByTelId(data.ChatId) == null)
        {
            collection = 
            [
                [new(Messages.Setup,         CallBacks.Setup)],
                [new(Messages.JoinToCycle,   CallBacks.JoinToCycle)],
                [new(Messages.CurrentStatus, CallBacks.CurrentStatus)],
            ];
        }
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.StartSection}\\");
        await services.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: keyboard);
    }
    
    private async Task Start(UpdateData data)
    {
        switch (data.DataSeparated[2])
        {
            case CallBacks.Setup:
                await AskForLastPeriodStart(data);
                break;
            case CallBacks.Edit:
                await EditCheck(data);
                break;
            case CallBacks.CurrentStatus:
                await SendCurrentStatusList(data);
                break;
            case CallBacks.ReportStart:
                await AskReport(data, true);
                break;
            case CallBacks.ReportEnd:
                await AskReport(data, false);
                break;
            case CallBacks.AddToCycle:
                await AddToCycle(data);
                break;
            case CallBacks.JoinToCycle:
                await JoinToCyclePressed(data);
                break;
        }
    }
    
    #endregion

    #region setup

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
        var date = MainService.DateValidation(data.MessageText!);
        if (date == null)
        {
            await services.SendMessage(data.ChatId, Messages.InvalidDate);
            await services.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
            return;
        }

        var exists = await ctServices.SetLastStartDate(data.ChatId, (DateTime)date);
        if (exists) await services.SendMessage(data.ChatId, Messages.LastStartChanged);
        else await services.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
    }
    
    public async Task SaveCycleLength(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var length))
        {
            await services.SendMessage(data.ChatId, Messages.InvalidInteger);
            await services.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        var exists = await ctServices.SetCycleLength(data.ChatId, length);
        if (exists) await services.SendMessage(data.ChatId, Messages.CycleLengthChanged);
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
        
        var exists = await ctServices.SetPeriodLength(data.ChatId, length);
        if (exists) await services.SendMessage(data.ChatId, Messages.PeriodLengthChanged);
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
        
        if (ownerName != null) 
            message = string.Format(Messages.SetNotifyCompleteGuest, ownerName) + message;
        else
            await services.SendMessage(chatId, Messages.SetupComplete);
        
        await services.SendMessage(chatId, message, true);
    }
    
    #endregion

    #region edit
    
    private async Task EditCheck(UpdateData data, bool commited = false)
    {
        var (cycleLength, periodLength, lastPeriodStart, avgCycleLength, avgPeriodLength, followers) =
            await ctServices.LoadCycleDetail(data.ChatId);
        var message = string.Format(Messages.CurrentData, lastPeriodStart, cycleLength, periodLength, avgCycleLength, avgPeriodLength) +
                      string.Format(Messages.Followers, followers);
        await services.SendMessage(data.ChatId, message, addMainKeyboard: true);

        if (commited)
        {
            await services.SendMessage(data.ChatId, Messages.ChangesCommited, addMainKeyboard: true);
            return;
        }

        var collection = new List<List<Tuple<string, string>>>
        {
            new() { new(Messages.EditPeriodLength, CallBacks.EditPeriodLength), new(Messages.EditCycleLength, CallBacks.EditCycleLength) },
            new() { new(Messages.EditFollowers, CallBacks.EditFollowers), new(Messages.EditFollowing, CallBacks.EditFollowing) },
            new() { new(Messages.EditLastPeriod, CallBacks.EditLastPeriod) },
            new() { new(Messages.EditNotify, CallBacks.EditNotify) },
        };
        var keyboard = services.CreateKeyboard(inlineCollection: collection,
            callBackStart: $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\");

        await services.SendMessage(data.ChatId, Messages.EditCheck, replyMarkup: keyboard);
    }
    
    private async Task EditCycle(UpdateData data)
    {
        switch (data.DataSeparated[2])
        {
            case CallBacks.EditPeriodLength:
                await services.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditCycleLength:
                await services.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditFollowers:
                await RemoveFollowersList(data);
                break;
            case CallBacks.EditFollowing:
                await RemoveFollowingList(data);
                break;
            case CallBacks.EditLastPeriod:
                await AskForLastPeriodStart(data);
                break;
            case CallBacks.EditNotify:
                await LoadCycleList(data.ChatId, CallBacks.EditNotify);
                break;
        }

        await EditCheck(data, true);
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
    
    #endregion

    #region Status

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
        var date = $"{DateTime.Now:MM/dd/yyyy} - {MainService.ConvertGregorianToJalali(DateTime.Now)}";
        await services.SendMessage(receiver!.ChatId, string.Format(Messages.StatusForReceiver, date, owner!.Name, message), true);
    }

    #endregion

    #region Report

    private async Task AskReport(UpdateData data, bool isStart)
    {
        await services.ApproveKeyboardInline(data.ChatId,
            isStart ? Messages.DidItStart : Messages.DidItEnd,
            $"{CallBacks.Cycle}\\{(isStart ? CallBacks.ReportStart : CallBacks.ReportEnd)}\\");
    }
    
    private async Task Report(UpdateData data, bool isStart)
    {        
        var result = data.DataSeparated[2];
        switch (result)
        {
            case CallBacks.Yes:
                if (isStart) await ctServices.SetNewStartByTelId(data.ChatId, DateTime.Now);
                else await ctServices.SetNewEndByTelId(data.ChatId);
                await services.SendMessage(data.ChatId, Messages.SavedData, true);
                await Notify(data.ChatId, isStart);
                break;
            case CallBacks.No:
                if (isStart)
                {
                    await ctServices.SaveLastCycleHistory(data.ChatId);
                    await services.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
                }
                else await services.SendMessage(data.ChatId, Messages.Welcome, true);
                break;
        }
    }
    
    private async Task Notify(long chatId, bool isStart)
    {
        var users = await ctServices.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await services.SendMessage(user.ChatId, string.Format(isStart ? Messages.NotifyStart : Messages.NotifyEnd, $"{user.Name}(@{user.Username})"), true);
        }
    }
    
    #endregion

    #region Join
    
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
    
    #endregion
    
    #region General
    
    private readonly TimeSpan _notificationHour = new(12, 30, 0);
    
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
            var date = $"{DateTime.Now:MM/dd/yyyy} - {MainService.ConvertGregorianToJalali(DateTime.Now)}";
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

    #endregion
    
}