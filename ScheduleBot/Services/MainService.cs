using System.Globalization;
using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class MainService(ITelegramBotClient bot)
{
    #region BotServices
    
    public async Task SendMessage(long chatId, string message, bool addMainKeyboard = false, ReplyMarkup? replyMarkup = null)
    {
        await bot.SendMessage(chatId, message, replyMarkup: addMainKeyboard ? GetMainKeyboard() : replyMarkup);
    }
    
    public ReplyKeyboardMarkup GetMainKeyboard()
    {
        var collection = new List<List<string>>
        {
            new() { Messages.PeriodTrackerSymbol + Messages.PeriodTracker, Messages.AboutSymbol + Messages.About },
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

    #endregion

    #region Statics

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

    #endregion
}