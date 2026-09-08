using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class NotificationService(
    AppDbContext dbContext,
    MainService service,
    CycleTrackerService cycleTrackerService)
    : DatabaseService(dbContext, service)
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly MainService _service = service;
    private static readonly TimeSpan PeriodTrackerNotificationTime = new(12, 30, 0);

    public async Task<Guid> CreateNewReminder(long chatId, string notificationName, DateTime firstOccurrence, int unitType,
        int? unitCount, string reminderMessage)
    {
        var user = await GetUserByTelId(chatId);
        if (firstOccurrence <= GetIranDateTime()) throw new IOException(Errors.FirstOccurrencePassed);
        
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
            IsActive = true,
            CreateTime = GetIranDateTime(),
            StartTime = firstOccurrence,
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
                time = new DateTime(1, 1, 1);
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

    public async Task<string?> SetPeriodTrackerNotify(long chatId, int mode, Guid cycleId)
    {
        if (mode < 0 || mode >= Messages.NotifyModes.Count)
            throw new ArgumentOutOfRangeException(nameof(mode));

        var receiver = await GetUserByTelId(chatId)
                       ?? throw new InvalidOperationException("User was not found.");
        var cycle = await _dbContext.CycleDetails.FirstOrDefaultAsync(x => x.Id == cycleId)
                    ?? throw new InvalidOperationException("Cycle was not found.");
        var owner = await GetUserById(cycle.UserId)
                    ?? throw new InvalidOperationException("Cycle owner was not found.");

        var notification = await GetOrCreatePeriodTrackerNotification(cycle, owner);
        var access = await _dbContext.NotificationAccess.FirstOrDefaultAsync(x =>
            x.NotificationId == notification.Id && x.UserId == receiver.Id);

        if (access == null)
        {
            _dbContext.NotificationAccess.Add(new NotificationAccess
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                UserId = receiver.Id,
                NotifyMode = mode
            });
        }
        else
        {
            access.NotifyMode = mode;
        }

        await _dbContext.SaveChangesAsync();
        return receiver.Id == owner.Id ? null : owner.Name;
    }

    public async Task<List<User>> GetPeriodTrackerFollowersByChatId(long chatId)
    {
        var cycle = await cycleTrackerService.GetCycleByTelId(chatId);
        if (cycle == null) return [];

        return await (
            from notification in _dbContext.Notification
            join access in _dbContext.NotificationAccess on notification.Id equals access.NotificationId
            join user in _dbContext.Users on access.UserId equals user.Id
            where notification.SpecialBehavior == CallBacks.SpecialPeriodTracker
                  && notification.SpecialBehaviorTargetId == cycle.Id
            select user
        ).ToListAsync();
    }

    public async Task<List<(string UserName, Guid CycleId)>> GetPeriodTrackerFollowingByChatId(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null) return [];

        return (await (
            from access in _dbContext.NotificationAccess
            join notification in _dbContext.Notification on access.NotificationId equals notification.Id
            join cycle in _dbContext.CycleDetails on notification.SpecialBehaviorTargetId equals cycle.Id
            join owner in _dbContext.Users on cycle.UserId equals owner.Id
            where access.UserId == user.Id
                  && notification.SpecialBehavior == CallBacks.SpecialPeriodTracker
            select new { UserName = owner.Name!, CycleId = cycle.Id }
        ).ToListAsync()).Select(x => (x.UserName, x.CycleId)).ToList();
    }

    public async Task RemovePeriodTrackerReceiver(Guid cycleId, Guid receiverId)
    {
        await (
            from access in _dbContext.NotificationAccess
            join notification in _dbContext.Notification on access.NotificationId equals notification.Id
            where notification.SpecialBehavior == CallBacks.SpecialPeriodTracker
                  && notification.SpecialBehaviorTargetId == cycleId
                  && access.UserId == receiverId
            select access
        ).ExecuteDeleteAsync();
    }

    public async Task SendPeriodTrackerNotifications(Guid notificationId)
    {
        var notification = await _dbContext.Notification.FirstOrDefaultAsync(x =>
            x.Id == notificationId && x.SpecialBehavior == CallBacks.SpecialPeriodTracker);
        if (notification?.SpecialBehaviorTargetId == null) return;

        var cycle = await cycleTrackerService.GetCycleByCycleId(notification.SpecialBehaviorTargetId.Value);
        if (cycle == null) return;

        var owner = await GetUserById(cycle.UserId);
        if (owner == null) return;

        var recipients = await (
            from access in _dbContext.NotificationAccess
            join user in _dbContext.Users on access.UserId equals user.Id
            where access.NotificationId == notification.Id
            select new { Access = access, User = user }
        ).ToListAsync();

        var now = GetIranDateTime();
        var status = await cycleTrackerService.CreateStatusMessage(cycle.Id);
        foreach (var recipient in recipients.Where(x => ShouldNotifyToday(cycle, x.Access.NotifyMode, now)))
        {
            var date = $"{now:MM/dd/yyyy} - {MainService.ConvertGregorianToJalali(now)}";
            if (recipient.User.Id == owner.Id)
            {
                await _service.SendMessage(owner.ChatId, string.Format(Messages.StatusForOwner, date, status));
                await _service.ApproveKeyboardInline(
                    owner.ChatId,
                    cycle.LastEnd != null ? Messages.DidItStart : Messages.DidItEnd,
                    $"{CallBacks.Cycle}|{(cycle.LastEnd != null ? CallBacks.ReportStart : CallBacks.ReportEnd)}|");
            }
            else
            {
                await _service.SendMessage(recipient.User.ChatId,
                    string.Format(Messages.StatusForReceiver, date, owner.Name, status));
            }
        }
    }

    public async Task SendPeriodTrackerEvent(long ownerChatId, bool isStart)
    {
        var owner = await GetUserByTelId(ownerChatId);
        var cycle = await cycleTrackerService.GetCycleByTelId(ownerChatId);
        if (owner == null || cycle == null) return;

        var recipients = await (
            from notification in _dbContext.Notification
            join access in _dbContext.NotificationAccess on notification.Id equals access.NotificationId
            join user in _dbContext.Users on access.UserId equals user.Id
            where notification.SpecialBehavior == CallBacks.SpecialPeriodTracker
                  && notification.SpecialBehaviorTargetId == cycle.Id
                  && access.NotifyMode != 0
                  && user.Id != owner.Id
            select user
        ).ToListAsync();

        foreach (var recipient in recipients)
        {
            await _service.SendMessage(recipient.ChatId,
                string.Format(isStart ? Messages.NotifyStart : Messages.NotifyEnd, owner.FullName));
        }
    }

    private async Task<Notification> GetOrCreatePeriodTrackerNotification(CycleDetail cycle, User owner)
    {
        var notification = await _dbContext.Notification.FirstOrDefaultAsync(x =>
            x.SpecialBehavior == CallBacks.SpecialPeriodTracker && x.SpecialBehaviorTargetId == cycle.Id);
        if (notification != null) return notification;

        var now = GetIranDateTime();
        var firstOccurrence = now.Date.Add(PeriodTrackerNotificationTime);
        if (firstOccurrence <= now) firstOccurrence = firstOccurrence.AddDays(1);

        notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            IsActive = true,
            CreateTime = now,
            StartTime = firstOccurrence,
            Type = CallBacks.NotificationDay,
            SeparationValue = 1,
            Name = Messages.PeriodTracker,
            Message = string.Empty,
            SpecialBehavior = CallBacks.SpecialPeriodTracker,
            SpecialBehaviorTargetId = cycle.Id
        };

        _dbContext.Notification.Add(notification);
        _dbContext.NotificationFutureMessage.Add(new Future
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            Time = firstOccurrence,
            Message = null
        });

        return notification;
    }

    private static bool ShouldNotifyToday(CycleDetail cycle, int mode, DateTime now)
    {
        if (cycle.LastStart == null || cycle.CycleLength == null) return false;

        var daysSinceLastStart = (now.Date - cycle.LastStart.Value.Date).Days;
        var daysUntilNext = cycle.CycleLength.Value - daysSinceLastStart;
        var isInPeriod = cycle.LastEnd == null && daysSinceLastStart >= 0;
        var isWithinThreeDaysBeforePeriod = daysUntilNext is >= 0 and <= 3;

        return mode switch
        {
            1 => true,
            2 => now.DayOfWeek == DayOfWeek.Monday,
            4 => isWithinThreeDaysBeforePeriod || isInPeriod,
            _ => false
        };
    }
}
