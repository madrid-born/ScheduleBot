using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class CycleTrackerService(
    ITelegramBotClient bot,
    IServiceProvider serviceProvider,
    DatabaseService db,
    ILogger<CycleTrackerService> logger)
{
    private readonly TimeSpan _notificationHour = new TimeSpan(8, 0, 0);

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
                await SendCurrentStatusList(data);
                break;
            case Messages.KeyboardCurrentStatus:
                await JoinToCyclePressed(data);
                break;
            case Messages.KeyboardAddToCycle:
                await AddToCycle(data);
                break;
            case Messages.KeyboardJoinToCycle:
                await JoinToCyclePressed(data);
                break;
        }
    }

    private async Task AskReportStart(UpdateData data)
    {
        var keyboard = new InlineKeyboardMarkup
        ([[
            InlineKeyboardButton.WithCallbackData(Messages.Yes, $"{CallBacks.Cycle}\\{CallBacks.ReportStart}\\{CallBacks.Yes}"),
            InlineKeyboardButton.WithCallbackData(Messages.No, $"{CallBacks.Cycle}\\{CallBacks.ReportStart}\\{CallBacks.No}"),
        ]]);
        
        await bot.SendMessage(data.ChatId, Messages.DidItStart, replyMarkup: keyboard);
    }
    private async Task AskReportEnd(UpdateData data)
    {
        var keyboard = new InlineKeyboardMarkup
        ([[
            InlineKeyboardButton.WithCallbackData(Messages.Yes, $"{CallBacks.Cycle}\\{CallBacks.ReportEnd}\\{CallBacks.Yes}"),
            InlineKeyboardButton.WithCallbackData(Messages.No, $"{CallBacks.Cycle}\\{CallBacks.ReportEnd}\\{CallBacks.No}"),
        ]]);
        
        await bot.SendMessage(data.ChatId, Messages.DidItStart, replyMarkup: keyboard);
    }

    private async Task ReportStart(UpdateData data)
    {        
        var result = data.DataSeparated[2];
        switch (result)
        {
            case CallBacks.Yes:
                await db.SetNewStartByTelId(data.ChatId, DateTime.Now);
                await bot.SendMessage(data.ChatId, Messages.SavedData, replyMarkup: MessageHandler.GetMainKeyboard());
                await StartNotify(data.ChatId);
                break;
            case CallBacks.No:
                await db.SaveLastCycleHistory(data.ChatId);
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
                await db.SetNewEndByTelId(data.ChatId);
                await bot.SendMessage(data.ChatId, Messages.SavedData, replyMarkup: MessageHandler.GetMainKeyboard());
                await EndNotify(data.ChatId);
                break;
            case CallBacks.No:
                await bot.SendMessage(data.ChatId, Messages.Welcome, replyMarkup: MessageHandler.GetMainKeyboard());
                break;
        }
    }

    private async Task StartNotify(long chatId)
    {
        var users = await db.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await bot.SendMessage(user.ChatId, string.Format(Messages.NotifyStart, $"{user.Name}({user.Username})"), replyMarkup: MessageHandler.GetMainKeyboard());
        }
    }
    
    private async Task EndNotify(long chatId)
    {
        var users = await db.GetFollowersByChatId(chatId);
        foreach (var user in users.Where(user => user.ChatId != chatId))
        {
            await bot.SendMessage(user.ChatId, string.Format(Messages.NotifyEnd, $"{user.Name}({user.Username})"), replyMarkup: MessageHandler.GetMainKeyboard());
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
                await EditCycle(data);
                break;
            case CallBacks.EditNotify:
                await EditNotify(data);
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
        await bot.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: GetKeyboard());
    }

    private static ReplyKeyboardMarkup GetKeyboard()
    {
        return new ReplyKeyboardMarkup
            ([
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardReportStart)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardReportEnd)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardSetup)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardCurrentStatus)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardEdit)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardAddToCycle)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardJoinToCycle)],
            ])
            { ResizeKeyboard = true };
    }

    private async Task AskForLastPeriodStart(UpdateData data)
    {
        var existing = await db.GetCycleByTelId(data.ChatId);
        if (existing != null)
        {
            await bot.SendMessage(data.ChatId, Messages.AvailableCycle);
            return;
        }

        await bot.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SaveLastPeriodStart(UpdateData data)
    {
        var edit = await db.GetCycleByTelId(data.ChatId) != null;
        var date = DateValidation(data.MessageText);
        if (date == null)
        {
            await bot.SendMessage(data.ChatId, Messages.InvalidDate);
            await bot.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
            return;
        }

        if (edit) await db.SetStartDate(data.ChatId, (DateTime)date);
        else await db.AddNewCycle(data.ChatId, (DateTime)date);
        
        await bot.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
    }

    private static DateTime? DateValidation(string? dataMessageText)
    {
        DateTime? date = null;
        try
        {
            var firstDigit = dataMessageText![1..];
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
        var month = int.Parse(date.Substring(4, 2));
        var day = int.Parse(date.Substring(6, 2));
        return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
    }
    
    private static string ConvertGregorianToJalali(DateTime date)
    {
        var pc = new PersianCalendar();

        var year = pc.GetYear(date);
        var month = pc.GetMonth(date);
        var day = pc.GetDayOfMonth(date);

        return $"{year:D4}/{month:D2}/{day:D2}";
    }

    public async Task SaveCycleLength(UpdateData data)
    {
        var edit = (await db.GetCycleByTelId(data.ChatId))!.CycleLength != null;

        if (!int.TryParse(data.MessageText, out var length))
        {
            await bot.SendMessage(data.ChatId, Messages.InvalidInteger);
            await bot.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        await db.SaveCycleLength(data.ChatId, length);
        if (!edit)
        {
            await bot.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
        }
    }

    public async Task SavePeriodLength(UpdateData data)
    {
        var edit = (await db.GetCycleByTelId(data.ChatId))!.PeriodLength != null;

        if (!int.TryParse(data.MessageText, out var length))
        {
            await bot.SendMessage(data.ChatId, Messages.InvalidInteger);
            await bot.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        await db.SavePeriodLength(data.ChatId, length);
        if (!edit)
        {
            await ShowNotifyModeMenu(data.ChatId);
        }
    }
    
    private async Task SelectCycleToChangeNotify(UpdateData data)
    {
        await LoadCycleList(data.ChatId, CallBacks.EditNotify);
    }
    
    private async Task EditNotify(UpdateData data)
    {
        var chatId = data.ChatId;
        var cycleId = Guid.Parse(data.DataSeparated[2]);
        await ShowNotifyModeMenu(chatId, cycleId);
    }

    private async Task ShowNotifyModeMenu(long chatId, Guid cycleId = default)
    {
        var keyboard = new InlineKeyboardMarkup(Enumerable.Range(0, Messages.NotifyModes.Count)
            .Select(i => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    Messages.NotifyModes[i],
                    $"{CallBacks.Cycle}\\{CallBacks.SetNotifyMode}\\{i}\\{cycleId}")
            })
        );

        await bot.SendMessage(chatId, Messages.AskForNotifyMode, replyMarkup: keyboard);
    }

    private async Task SetNotifyMode(UpdateData data)
    {
        var chatId = data.ChatId;
        var mode = int.Parse(data.DataSeparated[2]);
        var isParsed = Guid.TryParse(data.DataSeparated[3], out var cycleId);
        await db.SetNotify(data.ChatId, mode, cycleId);
        var message = string.Format(Messages.SetNotifyComplete, Messages.NotifyModes[mode]);
        if (isParsed)
        {
            var ownerName = (await db.GetCycleOwnerByCycleId(cycleId))!.Name;
            message = string.Format(Messages.SetNotifyCompleteGuest, ownerName, Messages.NotifyModes[mode]);
        }
        else
        {
            await bot.SendMessage(chatId, Messages.SetupComplete);
            await bot.SendMessage(chatId, Messages.EditPeriodReminder);
        }
        await bot.SendMessage(chatId, message, replyMarkup: MessageHandler.GetMainKeyboard());
    }
    
    private async Task EditCheck(UpdateData data, bool commited)
    {
        var cycleDetail = (await db.GetCycleByTelId(data.ChatId))!;
        var cycleHistories = (await db.GetCycleHistoryByCycleId(cycleDetail.Id))!;
        var cycleUsers = await db.GetNotifyUsersByCycleId(cycleDetail.Id);
        var lastPeriodStart = cycleDetail.LastStart + " " + ConvertGregorianToJalali((DateTime)cycleDetail.LastStart!);
        var cycleLength = cycleDetail.PeriodLength;
        var periodLength = cycleDetail.PeriodLength;
        var (avgCycleLength, avgPeriodLength) = CalculateAverages(cycleHistories);
        var followers = cycleUsers.Aggregate("", (current, user) => current + user!.Name + ", " + user.Username);
        var message = Messages.EditCheck;
        message += string.Format(Messages.CurrentData, lastPeriodStart, cycleLength, periodLength, avgCycleLength, avgPeriodLength);
        message += string.Format(Messages.Followers, followers);
        
        var keyboard = new InlineKeyboardMarkup(
            [
                [InlineKeyboardButton.WithCallbackData(Messages.EditPeriodLength, $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\{CallBacks.EditPeriodLength}")],
                [InlineKeyboardButton.WithCallbackData(Messages.EditCycleLength, $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\{CallBacks.EditCycleLength}")],
                [InlineKeyboardButton.WithCallbackData(Messages.EditFollowers, $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\{CallBacks.EditFollowers}")],
                [InlineKeyboardButton.WithCallbackData(Messages.EditLastPeriod, $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\{CallBacks.EditLastPeriod}")],
                [InlineKeyboardButton.WithCallbackData(Messages.EditNotify, $"{CallBacks.Cycle}\\{CallBacks.EditSection}\\{CallBacks.EditNotify}")],
            ]
        );
        
        await bot.SendMessage(data.ChatId, message, replyMarkup: commited ? MessageHandler.GetMainKeyboard() : keyboard);
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
                await bot.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditCycleLength:
                await bot.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.EditNotify:
                await SelectCycleToChangeNotify(data);
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
        var result = (await db.GetCycleByTelId(data.ChatId))!.Id;
        await bot.SendMessage(data.ChatId, string.Format(Messages.ShareCycleId, result), replyMarkup: MessageHandler.GetMainKeyboard());
    }
    
    private async Task JoinToCyclePressed(UpdateData data)
    {
        await bot.SendMessage(data.ChatId, Messages.AskForCycleId, replyMarkup: new ForceReplyMarkup());
    }
    
    public async Task JoinToCycleById(UpdateData data)
    {
        var flag = Guid.TryParse(data.MessageText, out var cycleId);
        if (flag && await db.GetCycleByCycleId(cycleId) != null)
        {
            await ShowNotifyModeMenu(data.ChatId, cycleId);
        }
        else
        {
            await bot.SendMessage(data.ChatId, Messages.CycleIdIsWrong, replyMarkup: MessageHandler.GetMainKeyboard());
        }
    }
    
    private async Task RemoveFollowingList(UpdateData data)
    {
        await LoadCycleList(data.ChatId, CallBacks.RemoveFollowing);
    }

    private async Task RemoveFollowersList(UpdateData data)
    {
        
        var keyboard = new InlineKeyboardMarkup((await db.GetFollowersByChatId(data.ChatId))
            .Select(user => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Name!,
                    $"{CallBacks.Cycle}\\{CallBacks.RemoveFollower}\\{user.Id}")
            })
        );

        await bot.SendMessage(data.ChatId, Messages.SelectUser, replyMarkup: keyboard);
    }
    
    private async Task RemoveFollower(UpdateData data)
    {
        var cycleId = (await db.GetCycleByTelId(data.ChatId))!.Id;
        var owner = await db.GetUserByTelId(data.ChatId);
        var receiver = await db.GetUserById(Guid.Parse(data.DataSeparated[2]));
        await db.RemoveReceiverFromCycle(cycleId, receiver!.Id);
        
        await bot.SendMessage(receiver.ChatId, string.Format(Messages.RemoveFollowerForReceiver, owner!.Name), replyMarkup: MessageHandler.GetMainKeyboard());
        await bot.SendMessage(owner!.ChatId, string.Format(Messages.RemoveFollowerForOwner, receiver.Name), replyMarkup: MessageHandler.GetMainKeyboard());
    }
    
    private async Task RemoveFollowing(UpdateData data)
    {
        var cycleId = Guid.Parse(data.DataSeparated[2]);
        var owner = await db.GetCycleOwnerByCycleId(cycleId);
        var receiver = await db.GetUserByTelId(data.ChatId);
        await db.RemoveReceiverFromCycle(cycleId, receiver!.Id);
        
        await bot.SendMessage(receiver.ChatId, string.Format(Messages.RemoveFollowingForReceiver, owner!.Name), replyMarkup: MessageHandler.GetMainKeyboard());
        await bot.SendMessage(owner!.ChatId, string.Format(Messages.RemoveFollowingForOwner, receiver.Name), replyMarkup: MessageHandler.GetMainKeyboard());
    }
    
    public async Task<string?> CreateStatusMessage(Guid cycleId)
    {
        var cycleDetail = await db.GetCycleByCycleId(cycleId);
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
        var owner = await db.GetCycleOwnerByCycleId(cycleId);
        var receiver = await db.GetUserByTelId(data.ChatId);
        var message = await CreateStatusMessage(cycleId);
        var date = $"{DateTime.Now:MM/dd/yyyy} - {ConvertGregorianToJalali(DateTime.Now)}";
        await bot.SendMessage(receiver!.ChatId, string.Format(Messages.StatusForReceiver, date, owner!.Name, message), replyMarkup: MessageHandler.GetMainKeyboard());
    }
    
    private async Task LoadCycleList(long chatId, string callBack)
    {
        var keyboard = new InlineKeyboardMarkup((await db.GetFollowingByChatId(chatId))
            .Select(user => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.UserName,
                    $"{CallBacks.Cycle}\\{callBack}\\{user.CycleId}")
            })
        );

        await bot.SendMessage(chatId, Messages.SelectCycle, replyMarkup: keyboard);
    }

    
    public async Task CheckAndSendNotifications()
    {
        // var now = DateTime.UtcNow;
        // if (now.TimeOfDay.Hour != _notificationHour.Hours || now.TimeOfDay.Minute != _notificationHour.Minutes)
        //     return;
        //
        // var allCycles = await db.CycleDetails.Include(c => c.NotifySetting).ToListAsync();
        //
        // foreach (var cycle in allCycles)
        // {
        //     if (cycle.NotifySetting == null) continue;
        //     
        //     var shouldNotify = ShouldNotifyToday(cycle, cycle.NotifySetting.NotifyMode);
        //     if (shouldNotify)
        //     {
        //         var message = GenerateNotificationMessage(cycle);
        //         await bot.SendMessage(cycle.UserId, message);
        //     }
        // }
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

    private string GenerateNotificationMessage(CycleDetail cycle)
    {
        return "result";
        // var today = DateTime.UtcNow.Date;
        // var daysSinceLastStart = (today - cycle.LastStart.Date).Days;
        // var currentDay = (daysSinceLastStart % cycle.CycleLength) + 1;
        // var daysUntilNext = cycle.CycleLength - daysSinceLastStart;
        // var isOnPeriod = today >= cycle.LastStart.Date && today <= cycle.LastEnd.Date;
        //
        // if (isOnPeriod)
        //     return $"🔴 You're on day {currentDay} of your period.\nRemaining: {cycle.PeriodLength - currentDay + 1} days";
        //
        // if (daysUntilNext <= 3)
        //     return $"⚠️ Your period starts in {daysUntilNext} days! (Day {currentDay} of cycle)";
        //
        // return $"📊 Day {currentDay} of your cycle\n📈 {GetCyclePhase(currentDay, cycle.CycleLength)}\n⏳ {daysUntilNext} days until next period";
    }
}