using System.Globalization;
using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class MainService(ITelegramBotClient bot)
{

    #region Statics

    public string Url { get; set; }
    public string BotToken { get; set; }
    public long AdminChatId { get; set; }

    #endregion
    
    #region BotServices
    
    public async Task<int> SendMessage(long chatId, string message, bool addMainKeyboard = false, ReplyMarkup? replyMarkup = null,ParseMode parseMode = ParseMode.Markdown)
    {
        return (await bot.SendMessage(chatId, message, replyMarkup: replyMarkup ?? GetMainKeyboard(), parseMode: parseMode)).MessageId;
    }
    
    public ReplyKeyboardMarkup GetMainKeyboard()
    {
        var collection = new List<List<string>>
        {
            new() { Messages.PeriodTrackerSymbol + Messages.PeriodTracker, Messages.CartSymbol + Messages.Cart },
            new() { Messages.TransactionSymbol + Messages.Transaction, },
        };
        
        return (ReplyKeyboardMarkup)CreateKeyboard(collection, resizeKeyboard: true);
    }
    
    public ReplyMarkup CreateKeyboard(IEnumerable<IEnumerable<string>>? normalCollection = null,IEnumerable<IEnumerable<Tuple<string, string>>>? inlineCollection = null,
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
        else
        {
            var keyboard = normalCollection!
                .Select(row => row
                    .Select(text => new KeyboardButton(symbol + text))
                    .ToArray())
                .ToArray();

            return new ReplyKeyboardMarkup(keyboard){ResizeKeyboard = resizeKeyboard};
        }
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

    #endregion

    #region StaticMethods

    public static DateTime? DateValidation(string dataMessageText)
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
        return pc.ToDateTime(year, month, day, hour, minute, second, 0);
    }
    
    public static string ConvertGregorianToJalali(DateTime date)
    {
        var pc = new PersianCalendar();

        var year = pc.GetYear(date);
        var month = pc.GetMonth(date);
        var day = pc.GetDayOfMonth(date);

        return $"{year:D4}/{month:D2}/{day:D2}";
    }
    public static string ConvertGregorianToJalaliWithTime(DateTime date)
    {
        return $"{ConvertGregorianToJalali(date)} {date.Hour}:{date.Minute}:{date.Second}";
    }

    #endregion
}