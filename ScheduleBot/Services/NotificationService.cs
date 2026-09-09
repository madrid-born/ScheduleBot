using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class NotificationService(
    AppDbContext dbContext,
    MainService service)
    : DatabaseService(dbContext, service)
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly MainService _service = service;

    public async Task<Guid> CreateNewReminder(long chatId, string notificationName, DateTime firstOccurrence, int unitType,
        int? unitCount, string reminderMessage)
    {
        var user = await GetUserByTelId(chatId);

        var firstTime = firstOccurrence;
        while (firstOccurrence < GetIranDateTime() && firstOccurrence != DateTime.MinValue)
            firstOccurrence = CalculateNextOccurrence(firstOccurrence, unitType, unitCount);
        
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
            IsActive = true,
            CreateTime = GetIranDateTime(),
            StartTime = firstTime,
            Type = unitType,
            SeparationValue = unitCount,
            Name = notificationName,
            Message = reminderMessage,
            SpecialBehavior = 0,
            SpecialBehaviorTargetId = null
        };
        
        var notificationAccess = new NotificationAccess
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = user.Id,
            NotifyMode = 0
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

    public async Task SetNotificationAccess(Guid notificationId, Guid receiverId, int mode)
    {
        _dbContext.NotificationAccess.Add(new NotificationAccess
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            UserId = receiverId,
            NotifyMode = mode
        });
        
        await _dbContext.SaveChangesAsync();
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
                NotificationId = notification.Id,
                ChatId = user.ChatId,
                Time = future.Time,
                Message = future.Message ?? notification.Message,
                SpecialBehavior =  notification.SpecialBehavior,
            };
        
        var result = await condition(query).ToListAsync();
        return result;
    }

    public async Task RenewFutureNotifications(Guid futureId)
    {
        var databaseFuture = await _dbContext.NotificationFutureMessage.FirstAsync(x => x.Id == futureId);
        var notification = await _dbContext.Notification.FirstAsync(x => x.Id == databaseFuture.NotificationId);

        databaseFuture.Message = null;
        while (databaseFuture.Time <= GetIranDateTime() && databaseFuture.Time != DateTime.MinValue)
        {
            databaseFuture.Time = CalculateNextOccurrence(databaseFuture.Time, notification.Type, notification.SeparationValue);
            if (databaseFuture.Time == DateTime.MinValue)
            {
                notification.IsActive = false;
                await _dbContext.NotificationFutureMessage.Where(x => x.Id == futureId).ExecuteDeleteAsync();
            }
        }
        
        await _dbContext.SaveChangesAsync();
    }
    
    private DateTime CalculateNextOccurrence(DateTime time, int type, int? separationValue)
    {
        switch (type)
        {
            case CallBacks.NotificationOneTime:
                time = DateTime.MinValue;
                break;
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
                    catch (Exception) { /* ignored */ }
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

    public async Task<NotificationAccess?> GetNotificationAccessByChatId(long chatId, Guid notificationId)
    {
        var user = await GetUserByTelId(chatId);
        return await _dbContext.NotificationAccess.FirstOrDefaultAsync(x =>
            x.NotificationId == notificationId && x.UserId == user.Id);
    }
}
