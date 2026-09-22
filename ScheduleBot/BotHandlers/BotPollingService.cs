using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace ScheduleBot.BotHandlers;

public class BotPollingService(
    ITelegramBotClient botClient,
    IServiceProvider serviceProvider,
    UserSessionService sessionService,
    ILogger<BotPollingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting bot polling service...");

        sessionService.SetData(0);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        // This application uses long polling, which Telegram will reject while a
        // webhook is configured. Clear any webhook left by an older deployment
        // before starting the receiver, while preserving pending updates.
        await botClient.DeleteWebhook(
            dropPendingUpdates: false,
            cancellationToken: stoppingToken);
        logger.LogInformation("Confirmed that no Telegram webhook is configured.");

        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation($"Bot started: @{me.Username}");

        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = serviceProvider.CreateScope();
                var notificationTracker = scope.ServiceProvider.GetRequiredService<NotificationHandler>();
                await notificationTracker.CheckAndSendNotifications();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }, stoppingToken);
        
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

    private async Task HandleErrorAsync(
        ITelegramBotClient botClient1,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Bot error occurred");

        if (exception is ApiRequestException { ErrorCode: 409 } &&
            exception.Message.Contains("webhook", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("A Telegram webhook conflict was detected. Removing the webhook and resuming polling.");

            try
            {
                await botClient1.DeleteWebhook(
                    dropPendingUpdates: false,
                    cancellationToken: cancellationToken);
                logger.LogInformation("Telegram webhook removed successfully. Polling will resume automatically.");
            }
            catch (Exception deleteWebhookException) when (deleteWebhookException is not OperationCanceledException)
            {
                logger.LogError(deleteWebhookException, "Failed to remove the conflicting Telegram webhook.");
            }
        }

        // Avoid a tight retry loop when Telegram or the network is unavailable.
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}
