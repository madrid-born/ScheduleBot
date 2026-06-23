using System.Globalization;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class CTH(ITelegramBotClient bot, IServiceProvider serviceProvider,
    MessageHandler mh, CycleTrackerService cts, ILogger<CycleTrackerHandler> logger)
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
        
        var keyboard = MessageHandler.CreateKeyboard(collection, symbol: Messages.PeriodTrackerSymbol, resizeKeyboard: true);

        await mh.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: keyboard);
    }
    
    private async Task AskReportStart(UpdateData data)
    {
        await mh.ApproveKeyboardInline(data.ChatId, Messages.DidItStart, $"{CallBacks.Cycle}\\{CallBacks.ReportStart}\\");
    }
    
    private async Task AskReportEnd(UpdateData data)
    {
        await mh.ApproveKeyboardInline(data.ChatId, Messages.DidItEnd, $"{CallBacks.Cycle}\\{CallBacks.ReportEnd}\\");
    }
    
    private async Task ReportStart(UpdateData data)
    {        
        var result = data.DataSeparated[2];
        switch (result)
        {
            case CallBacks.Yes:
                await mh.SendMessage(data.ChatId, Messages.SavedData, true);
                await cts.SetNewStartByTelId(data.ChatId, DateTime.Now);
                await StartNotify(data.ChatId);
                break;
            case CallBacks.No:
                await cts.SaveLastCycleHistory(data.ChatId);
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
                await cts.SetNewEndByTelId(data.ChatId);
                await mh.SendMessage(data.ChatId, Messages.SavedData, true);
                await EndNotify(data.ChatId);
                break;
            case CallBacks.No:
                await mh.SendMessage(data.ChatId, Messages.Welcome, true);
                break;
        }
    }

    private async Task StartNotify(long chatId)
    {
        var users = await cts.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await mh.SendMessage(user.ChatId, string.Format(Messages.NotifyStart, $"{user.Name}(@{user.Username})"), true);
        }
    }
    
    private async Task EndNotify(long chatId)
    {
        var users = await cts.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await mh.SendMessage(user.ChatId, string.Format(Messages.NotifyEnd, $"{user.Name}(@{user.Username})"), true);
        }
    }

    private async Task AskForLastPeriodStart(UpdateData data)
    {
        var existing = await cts.GetCycleByTelId(data.ChatId);
        if (existing != null)
        {
            await mh.SendMessage(data.ChatId, Messages.AvailableCycle, true);
            return;
        }
        
        await mh.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SaveLastPeriodStart(UpdateData data)
    {
        var date = DateValidation(data.MessageText!);
        if (date == null)
        {
            await mh.SendMessage(data.ChatId, Messages.InvalidDate);
            await mh.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
            return;
        }

        if (await cts.Seva(data.ChatId, (DateTime)date))
        {
            // todo
        }
        else await mh.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
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
            await mh.SendMessage(data.ChatId, Messages.InvalidInteger);
            await mh.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        if (await cts.Seva2(data.ChatId, length))
        {
            // todo
        }
        else await mh.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SavePeriodLength(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var length))
        {
            await mh.SendMessage(data.ChatId, Messages.InvalidInteger);
            await mh.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        if (await cts.Seva3(data.ChatId, length))
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

        var keyboard = MessageHandler.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.SetNotifyMode}\\");
        await mh.SendMessage(chatId, Messages.AskForNotifyMode, replyMarkup: keyboard);
    }

    private async Task SetNotifyMode(UpdateData data)
    {
        var chatId = data.ChatId;
        var mode = int.Parse(data.DataSeparated[2]);
        var cycleId = Guid.Parse(data.DataSeparated[3]);
        var ownerName = await cts.SetNotify(chatId, mode, cycleId);
        var message = string.Format(Messages.SetNotifyComplete, Messages.NotifyModes[mode]);
        if (ownerName != null) message = string.Format(Messages.SetNotifyCompleteGuest, ownerName) + message;
        else
        {
            await mh.SendMessage(chatId, Messages.SetupComplete);
            await mh.SendMessage(chatId, Messages.EditPeriodReminder);
        }
        await mh.SendMessage(chatId, message, true);
    }
    
    private async Task EditCheck(UpdateData data, bool commited = false)
    {
        var (cycleLength, periodLength, lastPeriodStart, avgCycleLength, avgPeriodLength, followers) =
            await cts.LoadCycleDetail(data.ChatId);
        var message = string.Format(Messages.CurrentData, lastPeriodStart, cycleLength, periodLength, avgCycleLength, avgPeriodLength) +
                      string.Format(Messages.Followers, followers);
        await mh.SendMessage(data.ChatId, message, addMainKeyboard: true);
        
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
            var keyboard = MessageHandler.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\");

            await mh.SendMessage(data.ChatId, Messages.EditCheck, replyMarkup: keyboard);
        }
    }

    private static (double AvgCycleLengthDays, double AvgPeriodLengthDays) CalculateAverages(IEnumerable<CycleHistory> cycleHistories)
    {
        var histories = cycleHistories.OrderBy(x => x.Start).ToList();

        if (histories.Count == 0) return (0, 0);

        var avgPeriodLengthDays = histories.Average(x => (x.End - x.Start).TotalDays);
        double avgCycleLengthDays = 0;

        if (histories.Count > 1)
        {
            avgCycleLengthDays = histories
                .Zip(histories, (current, next) => (next.Start - current.End).TotalDays)
                .Average();
        }

        return (avgCycleLengthDays, avgPeriodLengthDays);
    }
    
    private async Task EditCycle(UpdateData data)
    {
        switch (data.DataSeparated[2])
        {
            case CallBacks.EditLastPeriod:
                await AskForLastPeriodStart(data);
                break;
            case CallBacks.EditPeriodLength:
                await mh.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditCycleLength:
                await mh.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
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
        var result = (await cts.GetCycleByTelId(data.ChatId))!.Id;
        await mh.SendMessage(data.ChatId, string.Format(Messages.ShareCycleId, result), true);
    }
    
    private async Task JoinToCyclePressed(UpdateData data)
    {
        await mh.SendMessage(data.ChatId, Messages.AskForCycleId, replyMarkup: new ForceReplyMarkup());
    }
    
    public async Task JoinToCycleById(UpdateData data)
    {
        var flag = Guid.TryParse(data.MessageText, out var cycleId);
        if (flag && await cts.GetCycleByCycleId(cycleId) != null)
        {
            await ShowNotifyModeMenu(data.ChatId, cycleId);
        }
        else
        {
            await mh.SendMessage(data.ChatId, Messages.CycleIdIsWrong, true);
        }
    }
    
    private async Task RemoveFollowingList(UpdateData data)
    {
        await LoadCycleList(data.ChatId, CallBacks.RemoveFollowing);
    }

    private async Task RemoveFollowersList(UpdateData data)
    {
        
        var keyboard = new InlineKeyboardMarkup((await cts.GetFollowersByChatId(data.ChatId))
            .Select(user => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Name!,
                    $"{CallBacks.Cycle}\\{CallBacks.RemoveFollower}\\{user.Id}")
            })
        );

        await mh.SendMessage(data.ChatId, Messages.SelectUser, replyMarkup: keyboard);
    }
    
    private async Task RemoveFollower(UpdateData data)
    {
        var cycleId = (await cts.GetCycleByTelId(data.ChatId))!.Id;
        var owner = await cts.GetUserByTelId(data.ChatId);
        var receiver = await cts.GetUserById(Guid.Parse(data.DataSeparated[2]));
        await cts.RemoveReceiverFromCycle(cycleId, receiver!.Id);
        
        await mh.SendMessage(receiver.ChatId, string.Format(Messages.RemoveFollowerForReceiver, owner!.Name), true);
        await mh.SendMessage(owner!.ChatId, string.Format(Messages.RemoveFollowerForOwner, receiver.Name), true);
    }
    
    private async Task RemoveFollowing(UpdateData data)
    {
        var cycleId = Guid.Parse(data.DataSeparated[2]);
        var owner = await cts.GetCycleOwnerByCycleId(cycleId);
        var receiver = await cts.GetUserByTelId(data.ChatId);
        await cts.RemoveReceiverFromCycle(cycleId, receiver!.Id);
        
        await mh.SendMessage(receiver.ChatId, string.Format(Messages.RemoveFollowingForReceiver, owner!.Name), true);
        await mh.SendMessage(owner!.ChatId, string.Format(Messages.RemoveFollowingForOwner, receiver.Name), true);
    }
    
    public async Task<string?> CreateStatusMessage(Guid cycleId)
    {
        var cycleDetail = await cts.GetCycleByCycleId(cycleId);
        if (cycleDetail == null) return null;
        if (cycleDetail.LastStart == null) return Messages.NoCycleData;

        var cycleLength = (int)cycleDetail.CycleLength!;
        var periodLength = (int)cycleDetail.PeriodLength!;
        var lastStart = cycleDetail.LastStart;
        var lastEnd = cycleDetail.LastEnd;
        var now = DateTime.UtcNow.Date;
        var daysSinceStart = (now - lastStart.Value).Days + 1;
        if (daysSinceStart < 0) return Messages.InvalidFutureCycle;

        #region InPeriod

        if (lastEnd == null)
        {
            var progress = (double)daysSinceStart / periodLength;
            string phase, details;

            switch (progress)
            {
                case <= 0.25:
                    phase = Messages.EarlyPeriod;
                    details = Messages.EarlyPeriodDescription;
                    break;
                case <= 0.6:
                    phase = Messages.MidPeriod;
                    details = Messages.MidPeriodDescription;
                    break;
                case <= 0.85:
                    phase = Messages.LatePeriod;
                    details = Messages.LatePeriodDescription;
                    break;
                case <= 1:
                    phase = Messages.FinalPeriod;
                    details = Messages.FinalPeriodDescription;
                    break;
                default:
                    phase = Messages.ExtendedPeriod;
                    details = Messages.ExtendedPeriodDescription;
                    break;
            }
            
            var remainingDays = periodLength - daysSinceStart;
            return string.Format(Messages.InPeriodTemplate, phase, daysSinceStart, periodLength, details, remainingDays);
        }
        
        #endregion

        #region OutOfCycle

        var daysSinceEnd = (now - lastEnd.Value).Days + 1;

        if (daysSinceEnd >= cycleLength)
        {
            var daysLate = daysSinceStart - cycleLength;
            var severity = daysLate / 14.0;

            string tone, reason;

            switch (severity)
            {
                case <= 0.15:
                    tone = Messages.SlightlyLate;
                    reason = Messages.SlightlyLateReason;
                    break;
                case <= 0.5:
                    tone = Messages.ModeratelyLate;
                    reason = Messages.ModeratelyLateReason;
                    break;
                case <= 1.0:
                    tone = Messages.SignificantlyLate;
                    reason = Messages.SignificantlyLateReason;
                    break;
                default:
                    tone = Messages.HighlyIrregular;
                    reason = Messages.HighlyIrregularReason;
                    break;
            }
            
            var confidence = Math.Max(0, 100 - (int)(severity * 60));
            return string.Format(Messages.LateCycleTemplate, tone, daysLate, reason, confidence);
        }
        
        #endregion

        #region InCycle

        var phaseJitter = 2;
        var menstrualDay = 7;
        var ovulationDay = cycleLength / 2;
        var ovulationStart = ovulationDay - phaseJitter;
        var ovulationEnd = ovulationDay + phaseJitter;
        var lutealStart = ovulationEnd + 1;
        var lutealEnd = cycleLength - 5;
        var remaining = cycleLength - daysSinceEnd + 1;
        
        if (daysSinceEnd <= menstrualDay)
            return string.Format(Messages.MenstrualPhaseTemplate, daysSinceEnd, cycleLength, menstrualDay, menstrualDay - daysSinceEnd);
        
        if (daysSinceEnd < ovulationStart)
            return string.Format(Messages.FollicularPhaseTemplate, daysSinceEnd, cycleLength, ovulationStart, ovulationEnd, phaseJitter, ovulationStart - daysSinceEnd);
        
        if (daysSinceEnd >= ovulationStart && daysSinceEnd <= ovulationEnd)
            return string.Format(Messages.OvulationPhaseTemplate, daysSinceEnd, cycleLength, ovulationDay, phaseJitter, Math.Abs(daysSinceEnd - ovulationDay));
        
        if (daysSinceEnd >= lutealStart && daysSinceEnd <= lutealEnd)
            return string.Format(Messages.LutealPhaseTemplate, daysSinceEnd, cycleLength, cycleLength - daysSinceEnd);            
        
        return string.Format(Messages.PremenstrualPhaseTemplate, daysSinceEnd, cycleLength, remaining);
        
        #endregion

    }
    
    private async Task SendCurrentStatusList(UpdateData data)
    {
        await LoadCycleList(data.ChatId, CallBacks.CurrentStatus);
    }
    
    private async Task SendCurrentStatus(UpdateData data)
    {
        var cycleId = Guid.Parse(data.DataSeparated[2]);
        var owner = await cts.GetCycleOwnerByCycleId(cycleId);
        var receiver = await cts.GetUserByTelId(data.ChatId);
        var message = await CreateStatusMessage(cycleId);
        var date = $"{DateTime.Now:MM/dd/yyyy} - {ConvertGregorianToJalali(DateTime.Now)}";
        await mh.SendMessage(receiver!.ChatId, string.Format(Messages.StatusForReceiver, date, owner!.Name, message), true);
    }
    
    private async Task LoadCycleList(long chatId, string callBack)
    {
        var keyboard = new InlineKeyboardMarkup((await cts.GetFollowingByChatId(chatId))
            .Select(user => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.UserName,
                    $"{CallBacks.Cycle}\\{callBack}\\{user.CycleId}")
            })
        );

        await mh.SendMessage(chatId, Messages.SelectCycle, replyMarkup: keyboard);
    }
    
    public async Task CheckAndSendNotifications()
    {
        var now = DateTime.UtcNow;
        if (now.TimeOfDay.Hours != _notificationHour.Hours || now.TimeOfDay.Minutes != _notificationHour.Minutes) return;
        var allCycleNotifies = await cts.GetAllCycleNotifies();
        
        foreach (var cycleNotify in allCycleNotifies)
        {
            var shouldNotify = ShouldNotifyToday(cycleNotify.cycle, cycleNotify.notify.NotifyMode);
            if (shouldNotify)
            {
                var date = $"{DateTime.Now:MM/dd/yyyy} - {ConvertGregorianToJalali(DateTime.Now)}";
                var message = await CreateStatusMessage(cycleNotify.cycle.Id);
                await mh.SendMessage(cycleNotify.receiver.ChatId, string.Format(Messages.StatusForReceiver, date, cycleNotify.owner.Name, message), true);
            }
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