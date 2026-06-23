using ScheduleBot.BotHandlers;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;

public class CycleTrackerService(
    ITelegramBotClient bot,
    IServiceProvider serviceProvider,
    DatabaseService db,
    MessageHandler messageHandler,
    ILogger<CycleTrackerHandler> logger)
{
    public async Task<CycleDetail?> GetCycleByTelId(long chatId)
    {        
        return await db.GetCycleByTelId(chatId);
    }
    
    public async Task<bool> Seva(long chatId, DateTime dateTime)
    {        
        var edit = await db.GetCycleByTelId(chatId) != null;
        
        if (edit) await db.SetStartDate(chatId, dateTime);
        else await db.AddNewCycle(chatId, dateTime);

        return  edit;
    }
    
    public async Task<bool> Seva2(long chatId, int length)
    {        
        var edit = (await db.GetCycleByTelId(chatId))!.CycleLength != null;
        await db.SaveCycleLength(chatId, length);

        return  edit;
    }
    
    public async Task<bool> Seva3(long chatId, int length)
    {        
        var edit = (await db.GetCycleByTelId(chatId))!.PeriodLength != null;
        await db.SavePeriodLength(chatId, length);

        return  edit;
    }
    
    public async Task SetNewStartByTelId(long chatId, DateTime dateTime)
    {        
        await db.SetNewStartByTelId(chatId, dateTime);
    }
    
    public async Task SaveLastCycleHistory(long chatId)
    {        
        await db.SaveLastCycleHistory(chatId);
    }
    
    public async Task SetNewEndByTelId(long chatId)
    {        
        await db.SetNewEndByTelId(chatId);
    }
    
    public async Task<List<User>> GetFollowersByChatId(long chatId)
    {        
        return await db.GetFollowersByChatId(chatId);
    }
    
    public async Task<string?> SetNotify(long chatId, int mode, Guid cycleId = default)
    {        
        return await db.SetNotify(chatId, mode, cycleId);
    }

    public async Task<User?> GetCycleOwnerByCycleId(Guid cycleId)
    {
        return await db.GetCycleOwnerByCycleId(cycleId);
    }
    
    public async Task<List<CycleHistory>> GetCycleHistoryByCycleId(Guid cycleId)
    {
        return await db.GetCycleHistoryByCycleId(cycleId);
    }

    public async Task<List<User?>> GetNotifyUsersByCycleId(Guid cycleDetailId)
    {
        return await db.GetNotifyUsersByCycleId(cycleDetailId);
    }

    public async Task<CycleDetail?> GetCycleByCycleId(Guid cycleId)
    {
        return await db.GetCycleByCycleId(cycleId);
    }
    
    public async Task<User?> GetUserByTelId(long telId)
    {
        return await db.GetUserByTelId(telId);
    }
    
    public async Task<User?> GetUserById(Guid id)
    {
        return await db.GetUserById(id);
    }
    
    public async Task RemoveReceiverFromCycle(Guid cycleId, Guid receiverId)
    {
        await db.RemoveReceiverFromCycle(cycleId, receiverId);
    }

    public async Task<List<(string UserName, Guid CycleId)>> GetFollowingByChatId(long chatId)
    {
        return await db.GetFollowingByChatId(chatId);
    }

    public async Task<List<(CycleDetail cycle, CycleNotify notify, User owner, User receiver)>> GetAllCycleNotifies()
    {
        return await db.GetAllCycleNotifies();
    }

    public async Task<(int? cycleLength, int?periodLength, string lastPeriodStart, double avgCycleLength, double avgPeriodLength, string followers)> LoadCycleDetail(long chatId)
    {
        var cycleDetail = (await GetCycleByTelId(chatId))!;
        var lastStart = (DateTime)cycleDetail.LastStart!;
        var cycleLength = cycleDetail.CycleLength;
        var periodLength = cycleDetail.PeriodLength;
        var lastPeriodStart = $"\n{lastStart.Year}/{lastStart.Month}/{lastStart.Day} {CTH.ConvertGregorianToJalali((DateTime)cycleDetail.LastStart!)}";
        var (avgCycleLength, avgPeriodLength) = CalculateAverages(await GetCycleHistoryByCycleId(cycleDetail.Id));
        var followers = (await GetNotifyUsersByCycleId(cycleDetail.Id)).Aggregate("", (current, user) => current + user!.Name + "(@" + user.Username + ")\n");

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

}