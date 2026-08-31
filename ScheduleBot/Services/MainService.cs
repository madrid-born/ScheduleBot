using System.Globalization;
using ScheduleBot.BotHandlers;
using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class MainService(ITelegramBotClient bot,IServiceProvider serviceProvider, IConfiguration configuration, IWebHostEnvironment environment, UserSessionService sessionService)
{

    #region Statics

    private string Dop => environment.IsDevelopment() ? "Development" : "Production";
    public string Url => configuration[$"{Dop}:Url"]!;
    public string BotToken => configuration[$"{Dop}:BotToken"]!;
    public long AdminChatId => long.Parse(configuration["Telegram:AdminChatId"]!);
    private CycleTrackerHandler CycleTrackerHandler => serviceProvider.GetRequiredService<CycleTrackerHandler>();
    private TransactionHandler TransactionHandler => serviceProvider.GetRequiredService<TransactionHandler>();
    private NotificationHandler NotificationHandler => serviceProvider.GetRequiredService<NotificationHandler>();
    
    #endregion
    
    #region BotServices

    #region MessageHandler
    
    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case CallBacks.DatePicker:
                await ProcessDatePicker(data);
                break;
        }
    }
    
    public async Task<int> SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null, string? imageUrl = null, InputFileStream? document = null, ParseMode parseMode = ParseMode.Markdown)
    {
        message = message.Replace("_", "-");
        
        if (imageUrl != null)
            return (await bot.SendPhoto(chatId, photo: new InputFileUrl(imageUrl), caption: message, replyMarkup: replyMarkup ?? GetMainKeyboard(chatId == AdminChatId), parseMode: parseMode)).MessageId;
        
        if (document != null)
            return (await bot.SendDocument(chatId, document, caption: message, replyMarkup: replyMarkup ?? GetMainKeyboard(chatId == AdminChatId), parseMode: parseMode)).MessageId;
        
        return (await bot.SendMessage(chatId, message, replyMarkup: replyMarkup ?? GetMainKeyboard(chatId == AdminChatId), parseMode: parseMode)).MessageId;
    }
    
    public async Task EditMessage(long chatId, int messageId, string? message = null, ReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.Markdown)
    {
        if(!string.IsNullOrEmpty(message))
        {
            await bot.EditMessageText(chatId: chatId, messageId: messageId, text: message, replyMarkup: replyMarkup as InlineKeyboardMarkup, parseMode: parseMode);
        }
        else
        {
            await bot.EditMessageReplyMarkup(chatId: chatId, messageId: messageId, replyMarkup: replyMarkup as InlineKeyboardMarkup);
        }
    }
    
    public async Task DeleteMessage(long chatId, int messageId)
    {
        await bot.DeleteMessage(chatId, messageId);
    }
    
    #endregion

    #region KeyboardSection
    
    public ReplyKeyboardMarkup GetMainKeyboard(bool isAdmin = false)
    {
        var collection = new List<List<string>>
        {
            new() { Messages.PeriodTracker, Messages.Cart },
            new() { Messages.Transaction,   Messages.Notification},
        };
        if (isAdmin) collection.Add([Messages.Spotify]);
        
        return (ReplyKeyboardMarkup)CreateKeyboard(collection, resizeKeyboard: true);
    }
    
    public ReplyMarkup? CreateKeyboard(IEnumerable<IEnumerable<string>>? normalCollection = null,IEnumerable<IEnumerable<Tuple<string, string>>>? inlineCollection = null,
        string symbol = "", string callBackStart = "", bool resizeKeyboard = true)
    {
        if (inlineCollection != null)
        {
            var keyboard = inlineCollection
                .Select(row => row
                    .Select(tuple =>
                        InlineKeyboardButton.WithCallbackData(symbol + tuple.Item1, callBackStart + tuple.Item2))
                    .ToArray())
                .ToArray();

            return new InlineKeyboardMarkup(keyboard);
        }
        if (normalCollection != null)
        {
            var keyboard = normalCollection
                .Select(row => row
                    .Select(text => new KeyboardButton(symbol + text))
                    .ToArray())
                .ToArray();

            return new ReplyKeyboardMarkup(keyboard){ResizeKeyboard = resizeKeyboard};
        }

        return null;
    }
    
    public async Task ApproveKeyboardInline(long chatId, string message, string callBackStart)
    {
        var collection = new List<List<Tuple<string, string>>>
        {
            new() {new(Messages.Yes, CallBacks.Yes)},
            new() {new(Messages.No, CallBacks.No)},
        };
        
        var keyboard = CreateKeyboard(inlineCollection: collection, callBackStart: callBackStart);

        await SendMessage(chatId, message, replyMarkup: keyboard);
    }

    public List<List<string>> LoadCollectionForNormalKeyboard<T>(List<T> items, Func<T, string>? nameSelector = null,
        int width = 2, string prefix = "", string suffix = "")
    {
        var collection = new List<List<string>>();
        nameSelector ??= x => x.ToString();
        
        for (var index = 0; index < (double)items.Count/width ; index += 1)
        {
            List<string> row = [];
            for (var i = 0; i < width; i++)
            {
                var itemIndex = index * width + i;
                if (itemIndex < items.Count)
                {
                    row.Add(nameSelector(items[itemIndex]));
                }
            }
            collection.Add(row);
        }

        return collection;
    }

    public List<List<Tuple<string, string>>> LoadCollectionInPages<T>(List<T> items, string callBack, int pageNumber,
        Func<T, Guid> idSelector, Func<T, string> nameSelector, int width = 3, int height = 3)
    {
        List<List<Tuple<string, string>>> collection = [];
        var pageSize = width * height;

        for (var index = pageNumber * pageSize; index < pageNumber * pageSize + pageSize; index += height)
        {
            List<Tuple<string, string>> row = [];

            for (var i = 0; i < width; i++)
            {
                var item = index + i < items.Count ? items[index + i] : default;

                var name = item == null ? "-" : nameSelector(item);
                var id = item == null ? Guid.Empty : idSelector(item);

                row.Add(
                    new Tuple<string, string>(
                        name,
                        $"{callBack}|{id}"
                    )
                );
            }

            collection.Add(row);
        }

        collection.Add(
        [
            new(Messages.PreviousPage, $"{CallBacks.PreviousPage}|{callBack}|{pageNumber}"),
            new(pageNumber.ToString(), ""),
            new(Messages.NextPage, $"{CallBacks.NextPage}|{callBack}|{pageNumber}")
        ]);
        
        if (new List<string>{CallBacks.Show}.Contains(callBack))
        {
            collection.Add([new (Messages.All, $"{callBack}|{CallBacks.All}")]);
        }

        return collection;
    }

    public List<List<Tuple<string, string>>> LoadCollectionInScroller<T>(List<T> items,
        Func<T, Guid> idSelector, Func<T, string> nameSelector,
        Func<T, bool>? tempAddedSelector = null, Func<T, bool>? tempDeletedSelector = null,
        int width = 3)
    {
        tempAddedSelector ??= _ => false;
        tempDeletedSelector ??= _ => false;

        List<List<Tuple<string, string>>> collection = [];
        for (var index = 0; index < (double)items.Count/width ; index += 1)
        {
            List<Tuple<string, string>> row = [];
            for (var i = 0; i < width; i++)
            {
                var item = index * width + i < items.Count ? items[index * width + i] : default;
                
                var prefix = "";
                if (item != null)
                {
                    if (tempAddedSelector(item)) prefix = "🆕 ";
                    if (tempDeletedSelector(item)) prefix = "🗑 ";
                    row.Add(new Tuple<string, string>(prefix+nameSelector(item), $"{idSelector(item).ToString()}"));
                }
                else row.Add(new Tuple<string, string>("-", "-"));
            }
            collection.Add(row);
        }
        collection.Add([new (Messages.Done, $"{CallBacks.Done}"), new (Messages.Cancel, $"{CallBacks.Cancel}"),]);

        return collection;
    }
    
    public List<List<Tuple<string, string>>> LoadCollectionOneClicker<T>(List<T> items,
        Func<T, Guid>? idSelector = null, Func<T, string>? nameSelector = null, int width = 2, string prefixCallbackData = "")
    {

        List<List<Tuple<string, string>>> collection = [];
        for (var index = 0; index < (double)items.Count/width ; index += 1)
        {
            List<Tuple<string, string>> row = [];
            for (var i = 0; i < width; i++)
            {
                var item = index * width + i < items.Count ? items[index * width + i] : default;

                row.Add(item != null
                    ? nameSelector == null || idSelector == null
                        ? new Tuple<string, string>(item!.ToString(),
                            $"{prefixCallbackData}|{item.ToString()}")
                        : new Tuple<string, string>(nameSelector(item),
                            $"{prefixCallbackData}|{idSelector(item).ToString()}")
                    : new Tuple<string, string>("-", "-"));
            }
            collection.Add(row);
        }

        return collection;
    }
    
    public List<List<Tuple<string, string>>> LoadCollectionMultiSelect<T, TId>(List<T> items, List<TId> selectedItems, bool allSelected,
        Func<T, TId> idSelector, Func<T, string> nameSelector, int width = 3, string prefixCallbackData = "")
    {
        List<List<Tuple<string, string>>> collection = [];
        
        for (var index = 0; index < (double)items.Count/width ; index += 1)
        {
            List<Tuple<string, string>> row = [];
            for (var i = 0; i < width; i++)
            {
                var itemIndex = index * width + i;
                var item = itemIndex < items.Count ? items[itemIndex] : default;
                if (item == null)
                {
                    row.Add(new Tuple<string, string>("-", "-"));
                    continue;
                }
                
                var itemId = idSelector(item);
                var isSelected = selectedItems.Contains(itemId);
                var displayName = (isSelected ? "☑" : "") + $" {nameSelector(item)}";
                row.Add(new Tuple<string, string>(displayName, $"{(string.IsNullOrEmpty(prefixCallbackData) ? "" : $"{prefixCallbackData}|")}{CallBacks.MultipleSelectToggle}|{itemId}"));
            }
            collection.Add(row);
        }
        
        List<Tuple<string, string>> footer =
        [
            new(Messages.Cancel, CallBacks.Cancel),
            allSelected
                ? new Tuple<string, string>(Messages.DeselectAll, CallBacks.MultipleDeselectAll)
                : new Tuple<string, string>(Messages.SelectAll, CallBacks.MultipleSelectAll),
            new(Messages.Done, CallBacks.Done),
        ];
        collection.Add(footer);

        return collection;
    }

    #endregion

    #region DatePicker
    
    public async Task SendDatePicker(long chatId, int? messageId = null, string? method = null , string message = Messages.SelectDatePicker, DateTime? passedDate = null, bool isJalali = true, bool timeIncluded = true)
    {
        var fixedDate = passedDate ?? GetIranDateTime(hourZero: true);

        if (!string.IsNullOrEmpty(method))
        {
            var session = sessionService.GetOrSetData(chatId);
            session.ClearDatePicker();
            session.SetDatePicker(chatId, method, timeIncluded, isJalali, message, fixedDate);
        }
        else
        {
            var session = sessionService.GetData(chatId);
            var datePickerData = session.DatePickerSetup;
            if (datePickerData == null) return;
            timeIncluded = datePickerData.TimeIncluded;
            isJalali = datePickerData.IsJalali;
            message = datePickerData.Message;
            fixedDate = datePickerData.FixedDate;
        }

        Tuple<string, string> switchCalenderTuple;
        int year, month, day;
        var (hour, minute) = (fixedDate.Hour, fixedDate.Minute);
        if (isJalali)
        {
            (year, month, day) = LoadJalaliDateData(fixedDate);
            switchCalenderTuple = new Tuple<string, string>(Messages.SelectGregorianCalender, CallBacks.SelectGregorianCalender);
        }
        else
        {
            (year, month, day) = (fixedDate.Year, fixedDate.Month, fixedDate.Day);
            switchCalenderTuple = new Tuple<string, string>(Messages.SelectJalaliCalender, CallBacks.SelectJalaliCalender);
        }

        GregorianToSimplified(fixedDate);
        var collection = new List<List<Tuple<string, string>>>
        {
            new()
            { 
                new($"{year:D4}", CallBacks.SelectYear),
                new($"{month:D2}", CallBacks.SelectMonth),
                new($"{day:D2}", CallBacks.SelectDay),
            },
            new() { switchCalenderTuple, new(Messages.Done, $"{CallBacks.Done}") }
        };
        
        if (timeIncluded)
        {
            collection.Insert(0,
            [
                new($"{hour:D2}", CallBacks.SelectHour),
                new(":", "-"),
                new($"{minute:D2}", CallBacks.SelectMinute)
            ]);
        }
        
        var keyboard = CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.MainSection}|{CallBacks.DatePicker}|{CallBacks.DatePickerMianMenu}|");
        if (messageId == null) await SendMessage(chatId, message, replyMarkup: keyboard);
        else await EditMessage(chatId, (int)messageId, message, replyMarkup: keyboard);
        
    }

    private async Task ProcessDatePicker(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var messageId = data.MessageId;
        
        switch (data.DataSeparated[2])
        {
            case CallBacks.DatePickerMianMenu:
            {
                switch (data.DataSeparated[3])
                {
                    case CallBacks.SelectGregorianCalender:
                        session.SetDatePicker(isJalali: false);
                        await SendDatePicker(data.ChatId, messageId);
                        break;
                    case CallBacks.SelectJalaliCalender:
                        session.SetDatePicker(isJalali: true);
                        await SendDatePicker(data.ChatId, messageId);
                        break;
                    case CallBacks.SelectYear:
                    case CallBacks.SelectMonth:
                    case CallBacks.SelectDay:
                    case CallBacks.SelectHour:
                    case CallBacks.SelectMinute:
                        await SendDateSelector(data.ChatId, messageId, session, data.DataSeparated[3]);
                        break;
                    case CallBacks.Done:
                        await RetrieveDate(data.ChatId, messageId, session);
                        break;
                }
                break;
            }
            case CallBacks.SelectYear:
            case CallBacks.SelectMonth:
            case CallBacks.SelectDay:
            case CallBacks.SelectHour:
            case CallBacks.SelectMinute:
            {
                await SetDateSelector(data.ChatId, messageId, session, data.DataSeparated[2], data.DataSeparated[3]);
                break;
            }
        }
    }

    private async Task SendDateSelector(long chatId, int messageId, UserSession session, string callback)
    {
        int year;
        if (session.DatePickerSetup == null) return;
        
        var fixedDate = session.DatePickerSetup.FixedDate;
        if (session.DatePickerSetup.IsJalali) (year, _, _) = LoadJalaliDateData(fixedDate);
        else (year, _, _) = (fixedDate.Year, fixedDate.Month, fixedDate.Day);
        
        var yearLevel = session.DatePickerSetup.YearLevel;
        var collection = new List<List<Tuple<string, string>>>();
        var message = "";
        switch (callback)
        {
            case CallBacks.SelectYear:
            {
                message = Messages.SelectYear;
                var numbers = Enumerable.Range(0, 10).ToList();
                var yearValue = (yearLevel ?? year) / 10 * 10;
                numbers = numbers.Select(num => (yearValue + num) * (int)Math.Pow(10, 4 - yearValue.ToString().Length)).ToList();
                collection = LoadCollectionOneClicker(numbers, width:2);
                collection.Insert(0, [new(Messages.LevelUp, $"|{CallBacks.LevelUp}")]);
                break;
            }
            case CallBacks.SelectMonth:
            {
                message = Messages.SelectMonth;
                var numbers = Enumerable.Range(1, 12).ToList();
                collection = LoadCollectionOneClicker(numbers, width:3);
                break;
            }
            case CallBacks.SelectDay:
            {
                message = Messages.SelectDay;
                var numbers = Enumerable.Range(1, 31).ToList();
                collection = LoadCollectionOneClicker(numbers, width:6);
                break;
            }
            case CallBacks.SelectHour:
            {
                message = Messages.SelectHour;
                var numbers = Enumerable.Range(1, 24).ToList();
                collection = LoadCollectionOneClicker(numbers, width:6);
                break;
            }
            case CallBacks.SelectMinute:
            {
                message = Messages.SelectMinute;
                var numbers = Enumerable.Range(1, 60).ToList();
                collection = LoadCollectionOneClicker(numbers, width:5);
                break;
            }
        }
        
        var keyboard = CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.MainSection}|{CallBacks.DatePicker}|{callback}");
        await EditMessage(chatId, messageId, message, replyMarkup: keyboard);
    }

    private async Task SetDateSelector(long chatId, int messageId, UserSession session, string callback, string value)
    {
        if (session.DatePickerSetup == null) return;
        int year, month, day;
        var fixedDate = session.DatePickerSetup.FixedDate;
        if (session.DatePickerSetup.IsJalali) (year, month, day) = LoadJalaliDateData(fixedDate);
        else (year, month, day) = (fixedDate.Year, fixedDate.Month, fixedDate.Day);
        var (hour, minute) = (fixedDate.Hour, fixedDate.Minute);
        switch (callback)
        {
            case CallBacks.SelectYear:
            {
                year = year / 10 * 10;
                if (value == CallBacks.LevelUp)
                {
                    var yearLevelTimes10 = session.DatePickerSetup.YearLevel ?? year;
                    session.SetDatePicker(yearLevel: yearLevelTimes10 / 10);
                    await SendDateSelector(chatId, messageId, session, callback);
                    return;
                }
                
                if (session.DatePickerSetup.YearLevel is < 1000)
                {
                    session.SetDatePicker(yearLevel: int.Parse(value));
                    await SendDateSelector(chatId, messageId, session, callback);
                    return;
                }

                session.SetDatePicker(yearLevel: null);
                year = int.Parse(value);
                break;
            }
            case CallBacks.SelectMonth:
            {
                month = int.Parse(value);
                break;
            }
            case CallBacks.SelectDay:
            {
                day = int.Parse(value);
                break;
            }
            case CallBacks.SelectHour:
            {
                hour = int.Parse(value);
                break;
            }
            case CallBacks.SelectMinute:
            {
                minute = int.Parse(value);
                break;
            }
        }
        
        try
        {
            fixedDate = session.DatePickerSetup.IsJalali
                ? ConvertJalaliToGregorianWithData(year, month, day, hour, minute)
                : new DateTime(year, month, day, hour, minute, 0);
            session.SetDatePicker(fixedDate: fixedDate);
            await SendDatePicker(chatId, messageId);
        }
        catch (Exception e)
        {
            await EditMessage(chatId, messageId, Messages.DateNotValid);
            await SendDateSelector(chatId, messageId, session, callback);
        }
    }
    
    private async Task RetrieveDate(long chatId, int messageId, UserSession session)
    {
        if (session.DatePickerSetup == null) return;
        var fixedDate = session.DatePickerSetup.FixedDate;
        var method = session.DatePickerSetup.Method;
        await DeleteMessage(chatId, messageId);
        switch (method)
        {
            case DatePickerMethods.PeriodDateCycleTracker:
                await CycleTrackerHandler.SaveLastPeriodStart(chatId, fixedDate);
                break;
            
            case DatePickerMethods.CustomStartTransactionReport:
                await TransactionHandler.SetCustomPeriod(chatId, fixedDate, true);
                break;
            case DatePickerMethods.CustomEndTransactionReport:
                await TransactionHandler.SetCustomPeriod(chatId, fixedDate, false);
                break;
            case DatePickerMethods.NotificationFirstOccurrence:
                await NotificationHandler.SetFirstOccurrence(chatId, fixedDate, session.DatePickerSetup.IsJalali);
                break;
            default:
                throw new Exception(Messages.SomethingWentWrong);
        }
    }
    
    #endregion

    #endregion

    #region StaticMethods

    public DateTime GetIranDateTime(DateTime? dateTimeNull = null, bool hourZero = false)
    {
        var dateTime = dateTimeNull ?? DateTime.UtcNow;
        var timeZoneId = OperatingSystem.IsWindows()
            ? "Iran Standard Time"
            : "Asia/Tehran";
        dateTime = TimeZoneInfo.ConvertTimeFromUtc(
            dateTime,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)
        );

        if (hourZero)
        {
            dateTime = dateTime.AddHours(-dateTime.Hour);
            dateTime = dateTime.AddMinutes(-dateTime.Minute);
            dateTime = dateTime.AddSeconds(-dateTime.Second);
        }

        return dateTime;
    }
    
    public static string GregorianToSimplified(DateTime date)
    {
        return $"{date.Year}{date.Month:D2}{date.Day:D2}";
    }
    
    public static DateTime SimplifiedToGregorian(string date)
    {
        var year = int.Parse(date.Substring(0, 4));
        var month = int.Parse(date.Substring(4, 2));
        var day = int.Parse(date.Substring(6, 2));
        return new DateTime(year, month, day);
    }
    
    public static (DateTime Last, DateTime Current, DateTime Next) LoadJalaliFirstOfMonths(DateTime date)
    {
        var pc = new PersianCalendar();
        var (year, month, _) = LoadJalaliDateData(date);

        var current  = First(year, month);
        var previous = month == 1  ? First(year - 1, 12) : First(year, month - 1);
        var next     = month == 12 ? First(year + 1, 1)  : First(year, month + 1);

        return (previous, current, next);

        DateTime First(int y, int m) => pc.ToDateTime(y, m, 1, 0, 0, 0, 0);
    }

    public static Tuple<int, int, int> LoadJalaliDateData(DateTime date)
    {
        var pc = new PersianCalendar();
        var jalaliYear = pc.GetYear(date);
        var jalaliMonth = pc.GetMonth(date);
        var jalaliDay = pc.GetDayOfMonth(date);
        return new Tuple<int, int, int>(jalaliYear, jalaliMonth, jalaliDay);
    }
    
    public static DateTime ConvertJalaliToGregorian(string date)
    {
        var pc = new PersianCalendar();
        var year = int.Parse(date.Substring(0, 4));
        var month = int.Parse(date.Substring(5, 2));
        var day = int.Parse(date.Substring(8, 2));
        var hour = 0;
        var minute = 0;
        var second = 0;
        if (date.Length > 10)
        {
            hour = int.Parse(date.Substring(11, 2));
            minute = int.Parse(date.Substring(14, 2));
            second = int.Parse(date.Substring(17, 2));
        }
        return ConvertJalaliToGregorianWithData(year, month, day, hour, minute, second);
    }

    public static DateTime ConvertJalaliToGregorianWithData(int year = 0, int month = 0, int day = 0, int hour = 0, int minute = 0, int second = 0, int millisecond = 0)
    {
        var pc = new PersianCalendar();
        return pc.ToDateTime(year, month, day, hour, minute, second, millisecond);
    }
    
    public static string ConvertGregorianToJalali(DateTime date)
    {
        var (year, month, day) = LoadJalaliDateData(date);
        return $"{year:D4}/{month:D2}/{day:D2}";
    }
    
    public static string ConvertGregorianToJalaliAndGregorian(DateTime date)
    {
        return $"{date.Year:D4}/{date.Month:D2}/{date.Day:D2} - {ConvertGregorianToJalali(date)}";
    }
    
    public static string ConvertGregorianToJalaliAndGregorianWithTime(DateTime date)
    {
        return $"{date.Hour:D2}:{date.Minute:D2}:{date.Second:D2}\n{ConvertGregorianToJalaliAndGregorian(date)}";
    }
    
    public static string TruncateString(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }

    #endregion 
}