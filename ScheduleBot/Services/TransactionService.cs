using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class TransactionService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    private readonly AppDbContext _dbContext = dbContext;
    
    public async Task<Guid> CreateNewWallet(long chatId, string walletName)
    {
        var user = await GetUserByTelId(chatId);
        
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Name = walletName,
            CreatorId = user!.Id
        };
        
        var walletAccess = new WalletAccess
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            CategoryId = user.Id
        };
        
        _dbContext.Wallet.Add(wallet);
        _dbContext.WalletAccess.Add(walletAccess);
        await _dbContext.SaveChangesAsync();
        return wallet.Id;
    }
    public async Task<List<Wallet>> GetWalletsByTelId(long chatId = 0)
    {
        var user = await GetUserByTelId(chatId);
        var walletAccesses = await _dbContext.WalletAccess.Where(w => w.CategoryId == user!.Id).Select(w => w.WalletId).ToListAsync();
        return await _dbContext.Wallet.Where(w => walletAccesses.Contains(w.Id)).ToListAsync();
    }
}
