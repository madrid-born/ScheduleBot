using ScheduleBot.BotHandlers;
using ScheduleBot.Services;
using Telegram.Bot;

public class CycleTrackerHandler(
    ITelegramBotClient bot,
    IServiceProvider serviceProvider,
    DatabaseService db,
    MessageHandler messageHandler,
    ILogger<CycleTrackerHandler> logger)
{
    
}