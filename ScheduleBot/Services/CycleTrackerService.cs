using Microsoft.EntityFrameworkCore;
using ScheduleBot.BotHandlers;
using ScheduleBot.Models;
using Telegram.Bot;

namespace ScheduleBot.Services;

public class CycleTrackerService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    private readonly AppDbContext _dbContext = dbContext;

    #region CycleTracker
    
    public async Task<CycleDetail?> GetCycleByTelId(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        return await _dbContext.CycleDetails.FirstOrDefaultAsync(c => c.UserId == user!.Id);
    }
    
    public async Task<CycleDetail?> GetCycleByCycleId(Guid cycleId)
    {
        return await _dbContext.CycleDetails.FirstOrDefaultAsync(c => c.Id == cycleId);
    }
    
    public async Task<bool> SetLastStartDate(long chatId, DateTime dateTime)
    {        
        var edit = await GetCycleByTelId(chatId) != null;
        
        if (edit) await SetStartDate(chatId, dateTime);
        else await AddNewCycle(chatId, dateTime);

        return edit;
    }
    
    public async Task<bool> SetCycleLength(long chatId, int length)
    {        
        var edit = (await GetCycleByTelId(chatId))!.CycleLength != null;
        await SaveCycleLength(chatId, length);

        return edit;
    }
    
    public async Task<bool> SetPeriodLength(long chatId, int length)
    {        
        var edit = (await GetCycleByTelId(chatId))!.PeriodLength != null;
        await SavePeriodLength(chatId, length);

        return edit;
    }

    public async Task<List<CycleHistory>> GetCycleHistoryByCycleId(Guid cycleId)
    {
        return await _dbContext.CycleHistories.Where(c => c.CycleId == cycleId).ToListAsync();
    }

    public async Task<User?> GetCycleOwnerByCycleId(Guid cycleId)
    {
        var userId = (await GetCycleByCycleId(cycleId))!.UserId;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId)!;
    }

    public async Task<List<User>> GetFollowersByChatId(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        var cycle = await GetCycleByTelId(user!.ChatId);
        return await GetFollowersByCycleId(cycle!.Id);
    }
    
    public async Task<List<User>> GetFollowersByCycleId(Guid cycleId)
    {
        var receiverIds = (await _dbContext.CycleNotifies.Where(x => x.CycleId == cycleId).ToListAsync())
            .Select(x => x.ReceiverId);
        return await _dbContext.Users.Where(x => receiverIds.Contains(x.Id)).ToListAsync();
    }
    
    public async Task<List<CycleNotify>> GetCycleNotifiesByCycleId(Guid cycleId)
    {
        return await _dbContext.CycleNotifies.Where(c => c.CycleId == cycleId).ToListAsync();
    }
    
    public async Task<List<User?>> GetNotifyUsersByCycleId(Guid cycleDetailId)
    {
        var cycleNotifies = await GetCycleNotifiesByCycleId(cycleDetailId);
        var users = await _dbContext.Users.ToListAsync();
        return cycleNotifies
            .Select(cycleNotify => users.FirstOrDefault(x => x.Id == cycleNotify.ReceiverId))
            .ToList();
    }
    
    public async Task AddNewCycle(long chatId, DateTime lastStart)
    {
        var user = await GetUserByTelId(chatId);
        var cycleDetail = new CycleDetail
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
        };
        
        _dbContext.CycleDetails.Add(cycleDetail);
        await _dbContext.SaveChangesAsync();
        await SetStartDate(chatId, lastStart);
    }

    public async Task SetNewStartByTelId(long chatId, DateTime date)
    {
        await SetStartDate(chatId, date);
    }
    
    public async Task SetNewEndByTelId(long chatId)
    {
        var cycle = await GetCycleByTelId(chatId);
        cycle!.LastEnd = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        await SaveLastCycleHistory(chatId);
    }

    public async Task SaveLastCycleHistory(long chatId)
    {
        var cycle = await GetCycleByTelId(chatId);
        var lastHistoryNumber = (await _dbContext.CycleHistories.Where(x => x.CycleId == cycle!.Id).ToListAsync()).Max(x => x.Count);
        var cycleHistory = new CycleHistory
        {
            CycleId = cycle!.Id,
            Count = lastHistoryNumber!++,
            Start = cycle.LastStart!.Value,
            End = cycle.LastEnd!.Value
        };
        
        _dbContext.CycleHistories.Add(cycleHistory);
        await _dbContext.SaveChangesAsync();
    }

    public async Task SetStartDate(long telId, DateTime date)
    {
        var cycle = await GetCycleByTelId(telId);
        cycle!.LastStart = date;
        cycle.LastEnd = null;
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task SaveCycleLength(long chatId, int length)
    {
        var user = await GetUserByTelId(chatId);
        var cycleDetail = await _dbContext.CycleDetails.FirstOrDefaultAsync(c => c.UserId == user!.Id);
        if (cycleDetail == null) throw new Exception("No available cycle had been found");
        cycleDetail.CycleLength = length;
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task SavePeriodLength(long chatId, int length)
    {
        var user = await GetUserByTelId(chatId);
        var cycleDetail = await _dbContext.CycleDetails.FirstOrDefaultAsync(c => c.UserId == user!.Id);
        if (cycleDetail == null) throw new Exception("No available cycle had been found");
        cycleDetail.PeriodLength = length;
        var end = cycleDetail.LastStart?.AddDays(length);
        if (DateTime.Now > end)
        {
            cycleDetail.LastEnd = end;
            
            var cycleHistory = new CycleHistory
            {
                CycleId = cycleDetail.Id,
                Count = 1,
                Start = (DateTime)cycleDetail.LastStart!,
                End = (DateTime)end
            };
        
            _dbContext.CycleHistories.Add(cycleHistory);
        }
        await _dbContext.SaveChangesAsync();
    }

    public async Task<string?> SetNotify(long chatId, int mode, Guid cycleId = default)
    {
        string? name = null;
        var userId = (await GetUserByTelId(chatId))!.Id;
        if (cycleId == Guid.Empty)
        {
            cycleId = (await _dbContext.CycleDetails.FirstOrDefaultAsync(c => c.UserId == userId))!.Id;
            name = (await GetCycleOwnerByCycleId(cycleId))!.Name;
        }
        var notify = await _dbContext.CycleNotifies.FirstOrDefaultAsync(n => n.CycleId == cycleId && n.ReceiverId == userId);
        if (notify == null)
        {
            notify = new CycleNotify
            {
                Id = Guid.NewGuid(),
                CycleId = cycleId,
                ReceiverId = userId,
                NotifyMode = mode
            };
            _dbContext.CycleNotifies.Add(notify);
        }
        else
        {
            notify.NotifyMode = mode;
        }

        await _dbContext.SaveChangesAsync();
        return name;
    }
    
    public async Task RemoveReceiverFromCycle(Guid cycleId, Guid receiverId)
    {
        await _dbContext.CycleNotifies
            .Where(x => x.CycleId == cycleId && x.ReceiverId == receiverId)
            .ExecuteDeleteAsync();
    }
    
    public async Task<List<(string UserName, Guid CycleId)>> GetFollowingByChatId(long chatId)
    {
        var user = await GetUserByTelId(chatId);

        return (await
                (
                    from notify in _dbContext.CycleNotifies
                    join cycle in _dbContext.CycleDetails
                        on notify.CycleId equals cycle.Id
                    join owner in _dbContext.Users
                        on cycle.UserId equals owner.Id
                    where notify.ReceiverId == user!.Id
                    select new { UserName = owner.Name, CycleId = cycle.Id }
                )
                .ToListAsync()).Select(x => (x.UserName, x.CycleId))
            .ToList();
    }
    
    
    public async Task<List<(CycleDetail cycle, User owner, List<(CycleNotify notify, User receiver)> notifies)>> GetAllCycleNotifies()
    {
        return (await
                (
                    from notify in _dbContext.CycleNotifies
                    join cycle in _dbContext.CycleDetails on notify.CycleId equals cycle.Id
                    join owner in _dbContext.Users on cycle.UserId equals owner.Id
                    join receiver in _dbContext.Users on notify.ReceiverId equals receiver.Id
                    select new { cycle, notify, owner, receiver }
                )
                .ToListAsync())
            .Select(x => (x.cycle, x.notify, x.owner, x.receiver))
            .GroupBy(x => new { x.cycle, x.owner })
            .Select(g => (
                g.Key.cycle,
                g.Key.owner,
                notifies: g.Select(x => (x.notify, x.receiver)).ToList()
            ))
            .ToList();
    }
    
    #endregion
    public async Task<(int? cycleLength, int?periodLength, string lastPeriodStart, double avgCycleLength, double avgPeriodLength, string followers)> LoadCycleDetail(long chatId)
    {
        var cycleDetail = (await GetCycleByTelId(chatId))!;
        var lastStart = (DateTime)cycleDetail.LastStart!;
        var cycleLength = cycleDetail.CycleLength;
        var periodLength = cycleDetail.PeriodLength;
        var lastPeriodStart = $"\n{lastStart.Year}/{lastStart.Month}/{lastStart.Day} - {MainService.ConvertGregorianToJalali((DateTime)cycleDetail.LastStart!)}";
        var (avgCycleLength, avgPeriodLength) = CalculateAverages(await GetCycleHistoryByCycleId(cycleDetail.Id));
        var followers = (await GetNotifyUsersByCycleId(cycleDetail.Id)).Where(x => x!.ChatId != chatId).Aggregate("", (current, user) => current + user!.Name + " (@" + user.Username + ")\n");

        return (cycleLength, periodLength, lastPeriodStart, avgCycleLength, avgPeriodLength, followers);
    }
    
    private static (double AvgCycleLengthDays, double AvgPeriodLengthDays) CalculateAverages(IEnumerable<CycleHistory> cycleHistories)
    {
        var histories = cycleHistories.OrderBy(x => x.Start).ToList();

        if (histories.Count == 0) return (0, 0);

        var avgPeriodLengthDays = histories.Average(x => (x.End - x.Start).TotalDays);
        double avgCycleLengthDays = 0;

        if (histories.Count > 1)
        {
            avgCycleLengthDays = histories
                .Zip(histories, (current, next) => (next.Start - current.End).TotalDays)
                .Average();
        }

        return (avgCycleLengthDays, avgPeriodLengthDays);
    }

    public async Task<string?> CreateStatusMessage(Guid cycleId)
    {
        var cycleDetail = await GetCycleByCycleId(cycleId);
        if (cycleDetail == null) return null;
        if (cycleDetail.LastStart == null) return Messages.NoCycleData;

        var cycleLength = (int)cycleDetail.CycleLength!;
        var periodLength = (int)cycleDetail.PeriodLength!;
        var lastStart = cycleDetail.LastStart;
        var lastEnd = cycleDetail.LastEnd;
        var now = DateTime.UtcNow.Date;
        var daysSinceStart = (now - lastStart.Value).Days + 1;
        if (daysSinceStart < 0) return Messages.InvalidFutureCycle;

        #region InPeriod

        if (lastEnd == null)
        {
            var progress = (double)daysSinceStart / periodLength;
            string phase, details;

            switch (progress)
            {
                case <= 0.25:
                    phase = Messages.EarlyPeriod;
                    details = Messages.EarlyPeriodDescription;
                    break;
                case <= 0.6:
                    phase = Messages.MidPeriod;
                    details = Messages.MidPeriodDescription;
                    break;
                case <= 0.85:
                    phase = Messages.LatePeriod;
                    details = Messages.LatePeriodDescription;
                    break;
                case <= 1:
                    phase = Messages.FinalPeriod;
                    details = Messages.FinalPeriodDescription;
                    break;
                default:
                    phase = Messages.ExtendedPeriod;
                    details = Messages.ExtendedPeriodDescription;
                    break;
            }
            
            var remainingDays = periodLength - daysSinceStart;
            return string.Format(Messages.InPeriodTemplate, phase, daysSinceStart, periodLength, details, remainingDays);
        }
        
        #endregion

        #region OutOfCycle

        var daysSinceEnd = (now - lastEnd.Value).Days + 1;

        if (daysSinceEnd >= cycleLength)
        {
            var daysLate = daysSinceStart - cycleLength;
            var severity = daysLate / 14.0;

            string tone, reason;

            switch (severity)
            {
                case <= 0.15:
                    tone = Messages.SlightlyLate;
                    reason = Messages.SlightlyLateReason;
                    break;
                case <= 0.5:
                    tone = Messages.ModeratelyLate;
                    reason = Messages.ModeratelyLateReason;
                    break;
                case <= 1.0:
                    tone = Messages.SignificantlyLate;
                    reason = Messages.SignificantlyLateReason;
                    break;
                default:
                    tone = Messages.HighlyIrregular;
                    reason = Messages.HighlyIrregularReason;
                    break;
            }
            
            var confidence = Math.Max(0, 100 - (int)(severity * 60));
            return string.Format(Messages.LateCycleTemplate, tone, daysLate, reason, confidence);
        }
        
        #endregion

        #region InCycle

        var phaseJitter = 2;
        var menstrualDay = 7;
        var ovulationDay = cycleLength / 2;
        var ovulationStart = ovulationDay - phaseJitter;
        var ovulationEnd = ovulationDay + phaseJitter;
        var lutealStart = ovulationEnd + 1;
        var lutealEnd = cycleLength - 5;
        var remaining = cycleLength - daysSinceEnd + 1;
        string inCycleMessage;
        
        if (daysSinceEnd <= menstrualDay)
            inCycleMessage = string.Format(Messages.MenstrualPhaseTemplate, daysSinceEnd, cycleLength, menstrualDay, menstrualDay - daysSinceEnd);
        
        else if (daysSinceEnd < ovulationStart)
            inCycleMessage = string.Format(Messages.FollicularPhaseTemplate, daysSinceEnd, cycleLength, ovulationStart, ovulationEnd, phaseJitter, ovulationStart - daysSinceEnd);
        
        else if (daysSinceEnd >= ovulationStart && daysSinceEnd <= ovulationEnd)
            inCycleMessage = string.Format(Messages.OvulationPhaseTemplate, daysSinceEnd, cycleLength, ovulationDay, phaseJitter, Math.Abs(daysSinceEnd - ovulationDay));
        
        else if (daysSinceEnd >= lutealStart && daysSinceEnd <= lutealEnd)
            inCycleMessage = string.Format(Messages.LutealPhaseTemplate, daysSinceEnd, cycleLength, cycleLength - daysSinceEnd);            
        
        else 
            inCycleMessage = string.Format(Messages.PremenstrualPhaseTemplate, daysSinceEnd, cycleLength, remaining);

        return inCycleMessage;

        #endregion

    }
}