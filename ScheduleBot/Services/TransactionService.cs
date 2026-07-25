using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class TransactionService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    public async Task<Guid> CreateNewWallet(long chatId, string walletName)
    {
        throw new NotImplementedException();
    }
}