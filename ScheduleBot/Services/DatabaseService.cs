using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class DatabaseService(AppDbContext context)
{
    public async Task<User?> GetUserByTelId(long telId)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.TelId == telId);
    }
}