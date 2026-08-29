using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class NotificationService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    private readonly AppDbContext _dbContext = dbContext;
    public async Task<Guid> CreateNewReminder(long chatId, string notificationName, DateTime firstOccurrence, int unitType,
        int? unitCount, string reminderMessage)
    {
        var user = await GetUserByTelId(chatId);
        if (firstOccurrence <= DateTime.UtcNow) throw new IOException(Errors.FirstOccurrencePassed);
        
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
            IsActive = true,
            CreateTime = DateTime.UtcNow,
            StartTime = firstOccurrence,
            Type = unitType,
            SeparationValue = unitCount,
            Name = notificationName,
            Message = reminderMessage
        };
        
        var notificationAccess = new NotificationAccess
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = user.Id
        };

        var notificationFutureMessage = new Future
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            Time = firstOccurrence,
            Message = null
        };
        
        _dbContext.Notification.Add(notification);
        _dbContext.NotificationAccess.Add(notificationAccess);
        _dbContext.NotificationFutureMessage.Add(notificationFutureMessage);
        await _dbContext.SaveChangesAsync();
        return notification.Id;
    }
    
    public async Task<List<ToBeSentNotification>> GetFutureNotificationsByNotificationId(Guid notificationId)
    {
        var future = await _dbContext.NotificationFutureMessage.FirstOrDefaultAsync(n => n.NotificationId == notificationId);
        return await LoadToBeSentNotifications(query => query.Where(n => n.FutureNotificationId == future!.Id));
    }
    
    public async Task<List<ToBeSentNotification>> GetNotificationsForNextHour(DateTime now)
    {
        var endTime = now.AddHours(1);
        return await LoadToBeSentNotifications(query => query.Where(n => n.Time <= endTime));
    }

    private async Task<List<ToBeSentNotification>> LoadToBeSentNotifications(
        Func<IQueryable<ToBeSentNotification>, IQueryable<ToBeSentNotification>> condition)
    {
        var query =
            from future in _dbContext.NotificationFutureMessage
            join notification in _dbContext.Notification
                on future.NotificationId equals notification.Id
            join user in _dbContext.Users
                on notification.UserId equals user.Id
            where notification.IsActive
            select new ToBeSentNotification
            {
                FutureNotificationId = future.Id,
                ChatId = user.ChatId,
                Time = future.Time,
                Message = future.Message ?? notification.Message,
                NotificationType = notification.Type,
                SeparationValue = notification.SeparationValue ?? 0
            };
        
        var result = await condition(query).ToListAsync();
        await RenewFutureNotifications(result);
        return result;
    }
    
    private async Task RenewFutureNotifications(List<ToBeSentNotification> notifications)
    {
        var ids = notifications.Select(x => x.FutureNotificationId).ToList();
        var futures = await _dbContext.NotificationFutureMessage.Where(x => ids.Contains(x.Id)).ToListAsync();

        foreach (var future in futures)
        {
            var notification = notifications.First(x => x.FutureNotificationId == future.Id);
            future.Time = CalculateNextOccurrence(future.Time, notification.NotificationType, notification.SeparationValue);
            future.Message = null;
        }

        await _dbContext.SaveChangesAsync();
    }
    
    private DateTime CalculateNextOccurrence(DateTime time, int type, int? separationValue)
    {
        switch (type)
        {
            case CallBacks.NotificationHour:
                time = time.AddHours((double)separationValue!);
                break;
            case CallBacks.NotificationDay:
                time = time.AddDays((double)separationValue!);
                break;
            case CallBacks.NotificationMonthGregorian:
                time = time.AddMonths((int)separationValue!);
                break;
            case CallBacks.NotificationMonthJalali:
                var pc = new PersianCalendar();
                var initialTime = time;
                var jalaliDay = pc.GetDayOfMonth(time);
                var totalMonth = pc.GetMonth(time) + (int)separationValue!;
                var expectedMonth = totalMonth % 12; 
                var expectedYear = pc.GetYear(time) + expectedMonth/12;
                while (initialTime == time)
                {
                    try
                    {
                        time = pc.ToDateTime(expectedYear, expectedMonth, jalaliDay--, time.Hour, time.Minute, 0, 0);
                    }
                    catch (Exception e) { /*ignored*/ }
                }
                break;
        }
        return time;
    }

    public async Task<List<Notification>> GetNotificationsByTelId(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        return await _dbContext.Notification.Where(n => n.UserId == user!.Id).OrderBy(c => c.CreateTime).ToListAsync();
    }
}