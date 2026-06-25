using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class DatabaseService(AppDbContext dbContext)
{
    public async Task<User?> GetUserByTelId(long telId)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.ChatId == telId);
    }
    
    public async Task<User?> GetUserById(Guid id)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    #region Register
    
    public async Task UpdateUserAcceptance(long chatId, bool isAccepted)
    {
        var user = await GetUserByTelId(chatId);
        if (user != null)
        {
            if (isAccepted) user.IsAccepted = isAccepted;
            else dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
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
    
        dbContext.Users.Add(newUser);
        await dbContext.SaveChangesAsync();
    }

    public async Task InsertUserName(long chatId, string? name = "")
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.ChatId == chatId);
        if (user != null)
        {
            user.Name = name;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<User> InsertUserEmail(long chatId, string? email = "")
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.ChatId == chatId);
        if (user != null)
        {
            user.Email = email;
            await dbContext.SaveChangesAsync();
        }
        return user!;
    }

    #endregion
}