using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class DatabaseService(AppDbContext context)
{
    public async Task<User?> GetUserByTelId(long telId)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.ChatId == telId);
    }

    public async Task<User> InsertUser(long dataChatId, string name, string email, string username)
    {
        var newUser = new User
        {
            Id = Guid.Empty,
            ChatId = dataChatId,
            Name = name,
            Username = username,
            Email = email,
            IsAccepted = false
        };
        
        context.Users.Add(newUser);
        await context.SaveChangesAsync();

        return newUser;
    }

    public async Task UpdateUserAcceptance(long chatId, bool isAccepted)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.ChatId == chatId);
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
}