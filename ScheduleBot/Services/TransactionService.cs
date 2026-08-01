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
            UserId = user.Id
        };
        
        _dbContext.Wallet.Add(wallet);
        _dbContext.WalletAccess.Add(walletAccess);
        await _dbContext.SaveChangesAsync();
        return wallet.Id;
    }
    public async Task<List<Wallet>> GetWalletsByTelId(long chatId = 0)
    {
        var user = await GetUserByTelId(chatId);
        var walletAccesses = await _dbContext.WalletAccess.Where(w => w.UserId == user!.Id).Select(w => w.WalletId).ToListAsync();
        return await _dbContext.Wallet.Where(w => walletAccesses.Contains(w.Id)).ToListAsync();
    }

    public async Task<Wallet?> GetWalletForUser(Guid walletId, long chatId)
    {
        var user = await GetUserByTelId(chatId);
        return user == null ? null : await _dbContext.WalletAccess
            .Where(a => a.WalletId == walletId && a.UserId == user.Id)
            .Join(_dbContext.Wallet, a => a.WalletId, w => w.Id, (_, w) => w)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> InviteAccept(long chatId, Guid walletId)
    {
        var wallet = await _dbContext.Wallet.FindAsync(walletId);
        var user = await GetUserByTelId(chatId);
        if (wallet == null || user == null) return false;
        if (await _dbContext.WalletAccess.AnyAsync(a => a.WalletId == walletId && a.UserId == user.Id)) return false;
        _dbContext.WalletAccess.Add(new WalletAccess { Id = Guid.NewGuid(), WalletId = walletId, UserId = user.Id });
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<Category>> GetCategories(Guid walletId) => await _dbContext.WalletCategoryRelation
        .Where(r => r.WalletId == walletId).Join(_dbContext.WalletCategory, r => r.CategoryId, c => c.Id, (_, c) => c).ToListAsync();

    public async Task<Guid> AddCategory(long chatId, Guid walletId, string name)
    {
        if (await GetWalletForUser(walletId, chatId) == null) throw new UnauthorizedAccessException();
        var category = new Category { Id = Guid.NewGuid(), Name = name.Trim() };
        _dbContext.WalletCategory.Add(category);
        _dbContext.WalletCategoryRelation.Add(new CategoryRelation { Id = Guid.NewGuid(), WalletId = walletId, CategoryId = category.Id });
        await _dbContext.SaveChangesAsync();
        return category.Id;
    }

    public async Task<bool> AddTransaction(long chatId, Guid walletId, Guid categoryId, DateTime date, bool deposit, decimal amount, string title, long? documentNo = null)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null || await GetWalletForUser(walletId, chatId) == null || !await _dbContext.WalletCategoryRelation.AnyAsync(r => r.WalletId == walletId && r.CategoryId == categoryId)) return false;
        _dbContext.WalletTransactions.Add(new TransactionRecord { Id = Guid.NewGuid(), WalletId = walletId, CategoryId = categoryId, ConsumerId = user.Id, Date = date, IsDeposit = deposit, Amount = Math.Abs(amount), Title = title.Trim(), DocumentNo = documentNo });
        return await _dbContext.SaveChangesAsync() > 0;
    }
}
