using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class BotPollingService(
    ITelegramBotClient botClient,
    IServiceProvider serviceProvider,
    ILogger<BotPollingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting bot polling service...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation($"Bot started: @{me.Username}");

        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient1,
        Telegram.Bot.Types.Update update,
        CancellationToken cancellationToken)
    {
        long? chatId = null;
        try
        {
            chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
            using var scope = serviceProvider.CreateScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<MessageHandler>();
            await messageHandler.HandleUpdateAsync(botClient1, update, cancellationToken);
        }
        catch (Exception ex)
        {
            if (chatId != null)
                await botClient.SendMessage(chatId, $"Something went wrong {ex.Message}",
                    cancellationToken: cancellationToken);
            logger.LogError(ex, "Error handling update");
        }
    }

    private Task HandleErrorAsync(
        ITelegramBotClient botClient1,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Bot error occurred");
        return Task.CompletedTask;
    }
}