using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class DatabaseService(AppDbContext context)
{
    public async Task<User?> GetUserByTelId(long telId)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.ChatId == telId);
    }
    
    public async Task<User?> GetUserById(Guid id)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    #region Register
    
    public async Task UpdateUserAcceptance(long chatId, bool isAccepted)
    {
        var user = await GetUserByTelId(chatId);
        if (user != null)
        {
            if (isAccepted) user.IsAccepted = isAccepted;
            else context.Users.Remove(user);
            await context.SaveChangesAsync();
        }
    }

    public async Task InsertEmptyUser(long chatId, string? username)
    {
        var newUser = new User
        {
            Id = Guid.Empty,
            ChatId = chatId,
            Username = username,
            IsAccepted = false
        };
    
        context.Users.Add(newUser);
        await context.SaveChangesAsync();
    }

    public async Task InsertUserName(long chatId, string? name = "")
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.ChatId == chatId);
        if (user != null)
        {
            user.Name = name;
            await context.SaveChangesAsync();
        }
    }

    public async Task<User> InsertUserEmail(long chatId, string? email = "")
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.ChatId == chatId);
        if (user != null)
        {
            user.Email = email;
            await context.SaveChangesAsync();
        }
        return user!;
    }

    #endregion

    #region CycleTracker

    public async Task<CycleDetail?> GetCycleByTelId(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        return await context.CycleDetails.FirstOrDefaultAsync(c => c.UserId == user!.Id);
    }
    
    public async Task<CycleDetail?> GetCycleByCycleId(Guid cycleId)
    {
        return await context.CycleDetails.FirstOrDefaultAsync(c => c.Id == cycleId);
    }

    public async Task<List<CycleHistory>> GetCycleHistoryByCycleId(Guid cycleId)
    {
        return await context.CycleHistories.Where(c => c.CycleId == cycleId).ToListAsync();
    }

    public async Task<string> GetFollowersByCycleId(Guid cycleId)
    {
        var userId =  (await context.CycleDetails.FirstOrDefaultAsync(c => c.Id == cycleId))!.UserId;
        return (await context.Users.FirstOrDefaultAsync(u => u.Id == userId))!.Name!;
    }
    
    public async Task<List<CycleNotify>> GetCycleNotifiesByCycleId(Guid cycleId)
    {
        return await context.CycleNotifies.Where(c => c.CycleId == cycleId).ToListAsync();
    }

    public async Task<List<User?>> GetNotifyUsersByCycleId(Guid cycleDetailId)
    {
        var cycleNotifies = await GetCycleNotifiesByCycleId(cycleDetailId);
        var users = await context.Users.ToListAsync();
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
        
        context.CycleDetails.Add(cycleDetail);
        await context.SaveChangesAsync();
        await SetStartDate(chatId, lastStart);
    }

    public async Task SetStartDate(long telId, DateTime date)
    {
        var cycle = await GetCycleByTelId(telId);
        cycle!.LastStart = date;
        await context.SaveChangesAsync();
    }
    
    public async Task SaveCycleLength(long chatId, int length)
    {
        var user = await GetUserByTelId(chatId);
        var cycleDetail = await context.CycleDetails.FirstOrDefaultAsync(c => c.UserId == user!.Id);
        if (cycleDetail == null) throw new Exception("No available cycle had been found");
        cycleDetail.CycleLength = length;
        await context.SaveChangesAsync();
    }
    
    public async Task SavePeriodLength(long chatId, int length)
    {
        var user = await GetUserByTelId(chatId);
        var cycleDetail = await context.CycleDetails.FirstOrDefaultAsync(c => c.UserId == user!.Id);
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
        
            context.CycleHistories.Add(cycleHistory);
        }
        await context.SaveChangesAsync();
    }

    public async Task SetNotify(long chatId, int mode, Guid cycleId = default)
    {
        var userId = (await GetUserByTelId(chatId))!.Id;
        if (cycleId == Guid.Empty)
            cycleId = (await context.CycleDetails.FirstOrDefaultAsync(c => c.UserId == userId))!.Id;
        var notify = await context.CycleNotifies.FirstOrDefaultAsync(n => n.CycleId == cycleId && n.ReceiverId == userId);
        if (notify == null)
        {
            notify = new CycleNotify
            {
                Id = Guid.NewGuid(),
                CycleId = cycleId,
                ReceiverId = userId,
                NotifyMode = mode
            };
            context.CycleNotifies.Add(notify);
        }
        else
        {
            notify.NotifyMode = mode;
        }

        await context.SaveChangesAsync();
    }

    #endregion
}