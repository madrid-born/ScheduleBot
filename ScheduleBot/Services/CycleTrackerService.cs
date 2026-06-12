using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;
using Telegram.Bot;
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
            case Messages.PeriodTrackerMessage:
                await StartSection(data);
                break;
            case Messages.KeyboardSetup:
                await AskForLastPeriodStart(data);
                break;
            case Messages.KeyboardCheckNotifications:
                // await AskForLastPeriodStart(data);
                break;
        }
    }

    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case "Setup":
                await AskForLastPeriodStart(data);
                break;
            case "SetLastStart":
                await SaveLastPeriodStart(data);
                break;
            case "SetPeriodLength":
                await SavePeriodLength(data);
                break;
            case "EditPeriod":
                await ShowEditMenu(data.ChatId);
                break;
            case "NewPeriod":
                await RecordNewPeriodStart(data);
                break;
            case "ViewStatus":
                await SendCycleStatus(data.ChatId);
                break;
            case "SetNotifyMode":
                await SetNotifyMode(data);
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
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardSetup)],
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.KeyboardCheckNotifications)],
            ])
            { ResizeKeyboard = true };
    }

    public async Task AskForLastPeriodStart(UpdateData data)
    {
        var existing = await db.LoadCycle(data.ChatId);
        if (existing != null)
        {
            await bot.SendMessage(data.ChatId, Messages.AvailableCycle);
            return;
        }

        await bot.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SaveLastPeriodStart(UpdateData data)
    {
        var date = DateValidation(data.MessageText);
        if (date == null)
        {
            await bot.SendMessage(data.ChatId, Messages.InvalidDate);
            await bot.SendMessage(data.ChatId, Messages.SetupTracker, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        await db.AddNewCycle(data.ChatId, (DateTime)date);
        await bot.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
    }

    private DateTime? DateValidation(string? dataMessageText)
    {
        DateTime? date = null;
        var firstDigit = dataMessageText![1..];
        switch (firstDigit)
        {
            case "1":
                // TODO : make JalaliDate
                break;
            case "2":
                if (DateTime.TryParse(dataMessageText, out var gregorianDate))
                {
                    date = gregorianDate;
                }
                break;
        }

        return date;
    }

    public async Task SaveCycleLength(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var length))
        {
            await bot.SendMessage(data.ChatId, Messages.InvalidInteger);
            await bot.SendMessage(data.ChatId, Messages.AskForCycleLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        await db.SaveCycleLength(data.ChatId, length);
        await bot.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
    }

    public async Task SavePeriodLength(UpdateData data)
    {
        if (!int.TryParse(data.MessageText, out var length))
        {
            await bot.SendMessage(data.ChatId, Messages.InvalidInteger);
            await bot.SendMessage(data.ChatId, Messages.AskForPeriodLength, replyMarkup: new ForceReplyMarkup());
            return;
        }
        
        await db.SavePeriodLength(data.ChatId, length);
        await ShowNotifyModeMenu(data.ChatId);
    }

    private async Task ShowNotifyModeMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(Enumerable.Range(0, Messages.NotifyModes.Count)
            .Select(i => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    Messages.NotifyModes[i],
                    $"{CallBacks.Cycle}\\{CallBacks.SetNotifyMode}\\{i}")
            })
        );

        await bot.SendMessage(chatId, Messages.AskForNotifyMode, replyMarkup: keyboard);
    }

    private async Task SetNotifyMode(UpdateData data)
    {
        var chatId = data.ChatId;
        var mode = int.Parse(data.DataSeparated[2]);
        await db.SetNotify(data.ChatId, mode);
        await bot.DeleteMessage(data.ChatId, data.MessageId);
        await bot.SendMessage(chatId, Messages.SetupComplete, replyMarkup: MessageHandler.GetMainKeyboard());
        await bot.SendMessage(chatId, Messages.EditPeriodReminder, replyMarkup: MessageHandler.GetMainKeyboard());
        await bot.SendMessage(chatId, string.Format(Messages.SetNotifyComplete, Messages.NotifyModes[mode]),
            replyMarkup: MessageHandler.GetMainKeyboard());
    }

    public async Task ShowEditMenu(long chatId)
    {
        // var cycleDetail = await db.CycleDetails.FirstOrDefaultAsync(c => c.UserId == chatId);
        // if (cycleDetail == null)
        // {
        //     await bot.SendMessage(chatId, "You don't have a cycle tracker set up yet. Click the Period Tracker button to start.");
        //     return;
        // }
        //
        // var keyboard = new InlineKeyboardMarkup(new[]
        // {
        //     new[] { InlineKeyboardButton.WithCallbackData("📅 New period started", $"{CallBacks.Cycle}\\NewPeriod") },
        //     new[] { InlineKeyboardButton.WithCallbackData("🔄 Change cycle length", $"{CallBacks.Cycle}\\SetCycleLength\\custom") },
        //     new[] { InlineKeyboardButton.WithCallbackData("📊 Current status", $"{CallBacks.Cycle}\\ViewStatus") },
        //     new[] { InlineKeyboardButton.WithCallbackData("🔔 Change notification mode", $"{CallBacks.Cycle}\\SetNotifyMode\\menu") }
        // });
        //
        // await bot.SendMessage(chatId, "What would you like to edit?", replyMarkup: keyboard);
    }

    public async Task RecordNewPeriodStart(UpdateData data)
    {
        // await bot.SendMessage(data.ChatId, "When did your new period start? (YYYY-MM-DD):", replyMarkup: new ForceReplyMarkup());
        // // You'll need to capture this via HandleMessageAsync with a state
    }

    public async Task SendCycleStatus(long chatId)
    {
        // var cycleDetail = await db.CycleDetails.FirstOrDefaultAsync(c => c.UserId == chatId);
        // if (cycleDetail == null) return;
        //
        // var today = DateTime.UtcNow.Date;
        // var daysSinceLastStart = (today - cycleDetail.LastStart.Date).Days;
        // var currentDay = (daysSinceLastStart % cycleDetail.CycleLength) + 1;
        // var daysUntilNext = cycleDetail.CycleLength - daysSinceLastStart;
        // var isOnPeriod = today >= cycleDetail.LastStart.Date && today <= cycleDetail.LastEnd.Date;
        //
        // string phase = GetCyclePhase(currentDay, cycleDetail.CycleLength);
        // string status = isOnPeriod ? "🔴 You are on your period (Day {currentDay} of {cycleDetail.PeriodLength})" : 
        //                              $"🟢 Day {currentDay} of your cycle\n📈 Phase: {phase}\n⏳ {daysUntilNext} days until next period";
        //
        // await bot.SendMessage(chatId, status);
    }

    private string GetCyclePhase(int day, int cycleLength)
    {
        if (day <= 7) return "Menstrual phase";
        if (day <= 14) return "Follicular phase";
        if (day <= 16) return "Ovulation window";
        if (day <= cycleLength - 5) return "Luteal phase";
        return "Premenstrual phase";
    }

    private string GetModeName(int mode) => mode switch
    {
        1 => "daily",
        2 => "weekly",
        3 => "start & end only",
        4 => "pre-period + period",
        _ => "daily"
    };

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
        return true;
        // var today = DateTime.UtcNow.Date;
        // var daysSinceLastStart = (today - cycle.LastStart.Date).Days;
        // var currentDay = (daysSinceLastStart % cycle.CycleLength) + 1;
        // var daysUntilNext = cycle.CycleLength - daysSinceLastStart;
        // var isInPeriod = today >= cycle.LastStart.Date && today <= cycle.LastEnd.Date;
        // var daysBeforePeriod = daysUntilNext <= 3;
        //
        // return mode switch
        // {
        //     1 => true,
        //     2 => today.DayOfWeek == DayOfWeek.Monday,
        //     3 => currentDay == 1 || (daysSinceLastStart > 0 && currentDay == cycle.PeriodLength),
        //     4 => daysBeforePeriod || isInPeriod,
        //     _ => false
        // };
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