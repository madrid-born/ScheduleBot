using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class MainService(ITelegramBotClient bot)
{
    public ReplyKeyboardMarkup GetMainKeyboard()
    {
        var collection = new List<List<string>>
        {
            new() { Messages.PeriodTrackerSymbol + Messages.PeriodTracker, Messages.About },
        };
        
        return (ReplyKeyboardMarkup)CreateKeyboard(collection, resizeKeyboard: true);
    }

    public async Task SendMessage(long chatId, string message, bool addMainKeyboard = false, ReplyMarkup? replyMarkup = null)
    {
        await bot.SendMessage(chatId, message, replyMarkup: addMainKeyboard ? GetMainKeyboard() : replyMarkup);
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
}