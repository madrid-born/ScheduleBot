using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class NotificationService(AppDbContext dbContext)
{
    public async Task<List<Notification>> GetNotificationsByTelId(long chatId)
    {
        throw new NotImplementedException();
    }

    public async Task CreateNewReminder(string notificationName, DateTime firstOccurrence, int unitType, int? unitCount, string reminderMessage)
    {
        throw new NotImplementedException();
    }
}