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
        return await _dbContext.Notification
            .Where(n => n.UserId == user!.Id)
            .OrderBy(c => c.CreateTime)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetManageableNotificationsByTelId(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        return await _dbContext.Notification
            .Where(n => n.UserId == user!.Id && n.SpecialBehavior == 0)
            .OrderByDescending(c => c.CreateTime)
            .ToListAsync();
    }

    public async Task<NotificationManagementDetails?> GetNotificationForManagement(long chatId, Guid notificationId)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null) return null;

        return await _dbContext.Notification
            .Where(n => n.Id == notificationId && n.UserId == user.Id && n.SpecialBehavior == 0)
            .Select(n => new NotificationManagementDetails
            {
                Id = n.Id,
                IsActive = n.IsActive,
                Name = n.Name,
                Message = n.Message,
                Type = n.Type,
                SeparationValue = n.SeparationValue,
                NextTime = _dbContext.NotificationFutureMessage
                    .Where(f => f.NotificationId == n.Id)
                    .Select(f => (DateTime?)f.Time)
                    .FirstOrDefault(),
                NextMessage = _dbContext.NotificationFutureMessage
                    .Where(f => f.NotificationId == n.Id)
                    .Select(f => f.Message)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateNotificationName(long chatId, Guid notificationId, string name)
    {
        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null) return false;

        notification.Name = name;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNotificationMessage(long chatId, Guid notificationId, string message)
    {
        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null) return false;

        notification.Message = message;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNextOccurrence(long chatId, Guid notificationId, DateTime nextTime)
    {
        if (nextTime <= GetIranDateTime()) return false;

        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null) return false;

        var future = await _dbContext.NotificationFutureMessage
            .FirstOrDefaultAsync(f => f.NotificationId == notification.Id);
        if (future == null)
        {
            future = new Future
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                Time = nextTime
            };
            _dbContext.NotificationFutureMessage.Add(future);
        }
        else
        {
            future.Time = nextTime;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNextMessage(long chatId, Guid notificationId, string? message)
    {
        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null) return false;

        var future = await _dbContext.NotificationFutureMessage
            .FirstOrDefaultAsync(f => f.NotificationId == notification.Id);
        if (future == null) return false;

        future.Message = message;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateSeparationType(long chatId, Guid notificationId, int type)
    {
        if (type is < CallBacks.NotificationOneTime or > CallBacks.NotificationMonthJalali) return false;

        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null) return false;

        notification.Type = type;
        notification.SeparationValue = type == CallBacks.NotificationOneTime
            ? null
            : notification.SeparationValue is > 0 ? notification.SeparationValue : 1;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateSeparationValue(long chatId, Guid notificationId, int separationValue)
    {
        if (separationValue <= 0) return false;

        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null || notification.Type == CallBacks.NotificationOneTime) return false;

        notification.SeparationValue = separationValue;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleNotificationActive(long chatId, Guid notificationId)
    {
        var notification = await FindManagedNotification(chatId, notificationId);
        if (notification == null) return false;

        if (!notification.IsActive)
        {
            var future = await _dbContext.NotificationFutureMessage
                .FirstOrDefaultAsync(f => f.NotificationId == notification.Id);
            if (future == null || future.Time <= GetIranDateTime()) return false;
        }

        notification.IsActive = !notification.IsActive;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private async Task<Notification?> FindManagedNotification(long chatId, Guid notificationId)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null) return null;

        return await _dbContext.Notification.FirstOrDefaultAsync(n =>
            n.Id == notificationId && n.UserId == user.Id && n.SpecialBehavior == 0);
    }

    public async Task<NotificationAccess?> GetNotificationAccessByChatId(long chatId, Guid notificationId)
    {
        var user = await GetUserByTelId(chatId);
        return await _dbContext.NotificationAccess.FirstOrDefaultAsync(x =>
            x.NotificationId == notificationId && x.UserId == user.Id);
    }
}
