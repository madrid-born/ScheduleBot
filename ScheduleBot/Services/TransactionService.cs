using System.Collections;
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
    
    public async Task<Wallet?> GetWalletByWalletId(Guid walletId)
    {
        return await _dbContext.Wallet.FirstOrDefaultAsync(w => w.Id == walletId);
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

    public async Task<List<Category>> GetCategories(Guid walletId)
    {
        return await _dbContext.WalletCategory.Where(r => r.WalletId == walletId).ToListAsync();
    } 
    
    public async Task<bool> AddTransaction(long chatId, Guid walletId, Guid categoryId, DateTime date, bool deposit, decimal amount, string title, long? documentNo = null)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null || await GetWalletForUser(walletId, chatId) == null || !await _dbContext.WalletCategory.AnyAsync(r => r.WalletId == walletId && r.Id == categoryId)) return false;
        _dbContext.WalletTransactions.Add(new TransactionRecord { Id = Guid.NewGuid(), WalletId = walletId, CategoryId = categoryId, ConsumerId = user.Id, Date = date, IsDeposit = deposit, Amount = Math.Abs(amount), Title = title.Trim(), DocumentNo = documentNo });
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<List<Category>> GetCategoriesByWalletId(Guid walletId)
    {
        return await _dbContext.WalletCategory.Where(c => c.WalletId == walletId).OrderBy(c => c.CreateTime).ToListAsync();
    }

    public async Task<Guid> AddCategoryToWallet(Guid walletId, string name)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = name.Trim() , WalletId = walletId, CreateTime = DateTime.Now, TempAdded = true};
        _dbContext.WalletCategory.Add(category);
        await _dbContext.SaveChangesAsync();
        return category.Id;
    }
    
    public async Task<Tuple<List<string>, List<string>, List<string>>> LoadCategoryServiceChanges(Guid walletId)
    {
        var categories = await GetCategoriesByWalletId(walletId);
        var added = categories.Where(p => p is { TempAdded: true, TempDeleted: false }).ToList();
        var deleted = categories.Where(p => p is { TempAdded: false, TempDeleted: true }).ToList();
        var both = categories.Where(p => p is { TempAdded: true, TempDeleted: true }).ToList();
        return new Tuple<List<string>, List<string>, List<string>>(
            added.Select(x => x.Name!).ToList(),
            deleted.Select(x => x.Name!).ToList(),
            both.Select(x => x.Name!).ToList());
    }
    
    public async Task<bool> SubmitCategoryServiceChanges(Guid walletId)
    {
        var categories = await GetCategoriesByWalletId(walletId);
        var added = categories.Where(p => p is { TempAdded: true, TempDeleted: false });
        foreach (var category in added) category.TempAdded = false;
        return await _dbContext.WalletCategory.Where(p => p.TempDeleted).ExecuteDeleteAsync() + await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> CancelCategoryServiceChanges(Guid walletId)
    {
        var categories = await GetCategoriesByWalletId(walletId);
        var deleted = categories.Where(p => p.TempDeleted);
        foreach (var category in deleted) category.TempDeleted = false;
        return  await _dbContext.WalletCategory.Where(p => p.TempAdded).ExecuteDeleteAsync() + await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<User>> GetUsersWithAccessByWalletId(Guid walletId)
    {
        var walletAccess = await GetCartAccessByWalletId(walletId);
        return await GetUsersByIds(walletAccess.Select(x => x.UserId).ToList());
    }
    
    public async Task<List<WalletAccess>> GetCartAccessByWalletId(Guid walletId)
    {
        return await _dbContext.WalletAccess.Where(c => c.WalletId == walletId).ToListAsync();
    }

    public async Task<bool> DeleteCategoryFromWallet(Guid categoryId)
    {
        var category = await GetCategoryByCategoryId(categoryId);
        category!.TempDeleted = !category.TempDeleted;
        return await _dbContext.SaveChangesAsync() > 0;
    }

    private async Task<Category?> GetCategoryByCategoryId(Guid categoryId)
    {
        return await _dbContext.WalletCategory.FirstOrDefaultAsync(c => c.Id == categoryId);
    }
}
