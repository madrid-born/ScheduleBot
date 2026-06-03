namespace ScheduleBot.Services;

public class UserService(DatabaseService db)
{
    public async Task<bool> CheckUserStatusAsync(long chatId)
    {
        var user = await db.GetUserByTelId(chatId);
        return user is { IsAccepted: true };
    }
}