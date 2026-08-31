using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class CartService(AppDbContext dbContext, MainService service) : DatabaseService(dbContext, service)
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<Cart?> GetCartByCartId(Guid cartId)
    {
        return await _dbContext.Cart.FirstOrDefaultAsync(c => c.Id == cartId);
    }
    
    public async Task<List<CartItem>> GetProductsByCartId(Guid cartId)
    {
        return await _dbContext.CartItem.Where(c => c.CartId == cartId).OrderBy(c => c.CreateTime).ToListAsync();
    }
    
    public async Task<CartItem?> GetProductByProductId(Guid productId)
    {
        return await _dbContext.CartItem.FirstOrDefaultAsync(c => c.Id == productId);
    }
    
    public async Task<List<CartAccess>> GetCartAccessByCartId(Guid cartId)
    {
        return await _dbContext.CartAccess.Where(c => c.CartId == cartId).ToListAsync();
    }
    
    public async Task<List<Cart>> GetCartsByTelId(long chatId = 0)
    {
        var user = await GetUserByTelId(chatId);
        var cartAccesses = await _dbContext.CartAccess.Where(c => c.UserId == user!.Id).Select(c => c.CartId).ToListAsync();
        return await _dbContext.Cart.Where(c => cartAccesses.Contains(c.Id)).ToListAsync();
    }
    
    public async Task<Tuple<Cart, List<CartItem?>>> GetCartAndItemsByCartId2(Guid cartId)
    {
        var cart = await GetCartByCartId(cartId);
        var cartItems = await GetProductsByCartId(cartId);
        return new(cart!, cartItems!);
    }
    
    public async Task<List<User>> GetUsersWithAccessByCartId(Guid cartId)
    {
        var cartAccesses = await GetCartAccessByCartId(cartId);
        return await GetUsersByIds(cartAccesses.Select(x => x.UserId).ToList());
    }
    
    public async Task<Guid> CreateNewCart(long chatId, string cartName)
    {
        var user = await GetUserByTelId(chatId);
        
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            Name = cartName,
            CreatorId = user!.Id
        };
        
        var cartAccess = new CartAccess
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            UserId = user.Id
        };
        
        _dbContext.Cart.Add(cart);
        _dbContext.CartAccess.Add(cartAccess);
        await _dbContext.SaveChangesAsync();
        return cart.Id;
    }

    public async Task<bool> DeleteCart(long dataChatId, Guid cartId)
    {
        var user = await GetUserByTelId(dataChatId);
        var cart = await GetCartByCartId(cartId);
        if (cart!.CreatorId != user!.Id) return false;
        
        await _dbContext.CartItem
            .Where(c => c.CartId == cartId)
            .ExecuteDeleteAsync();
        await _dbContext.CartAccess
            .Where(c => c.CartId == cartId)
            .ExecuteDeleteAsync();
        var deleteCart = await _dbContext.Cart
            .Where(c => c.Id == cartId)
            .ExecuteDeleteAsync();
        return deleteCart > 0;
    }

    public async Task InviteAccept(long dataChatId, Guid cartId)
    {
        var user = await GetUserByTelId(dataChatId);
        var cart = await GetCartByCartId(cartId);

        var availableCartAccess = await _dbContext.CartAccess.FirstOrDefaultAsync(ca => ca.CartId == cart!.Id &&  ca.UserId == user!.Id);
        if (availableCartAccess != null) throw new IOException(Messages.RedundantAccess);
        var cartAccess = new CartAccess
        {
            Id = Guid.NewGuid(),
            CartId = cart!.Id,
            UserId = user!.Id
        };
        
        _dbContext.CartAccess.Add(cartAccess);
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task<bool> AddProductToCart(Guid cartId, string productName)
    {
        var cart = await GetCartByCartId(cartId);

        var cartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart!.Id,
            Name =  productName,
            CreateTime = GetIranDateTime(true),
            TempAdded = true,
            TempDeleted = false
        };
        
        _dbContext.CartItem.Add(cartItem);
        return await _dbContext.SaveChangesAsync() > 0;
    }
    
    public async Task<bool> DeleteProductFromCart(Guid productId)
    {
        var cartItem = await GetProductByProductId(productId);
        cartItem!.TempDeleted = !cartItem.TempDeleted;
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> SubmitProductServiceChanges(Guid cartId)
    {
        var products = await GetProductsByCartId(cartId);
        var added = products.Where(p => p is { TempAdded: true, TempDeleted: false });
        foreach (var product in added) product.TempAdded = false;
        return  await _dbContext.CartItem.Where(p => p.TempDeleted).ExecuteDeleteAsync() + await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> CancelProductServiceChanges(Guid cartId)
    {
        var products = await GetProductsByCartId(cartId);
        var deleted = products.Where(p => p.TempDeleted);
        foreach (var product in deleted) product.TempDeleted = false;
        return  await _dbContext.CartItem.Where(p => p.TempAdded).ExecuteDeleteAsync() + await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<Tuple<List<CartItem>, List<CartItem>, List<CartItem>>> LoadProductServiceChanges(Guid cartId)
    {
        var products = await GetProductsByCartId(cartId);
        var added = products.Where(p => p is { TempAdded: true, TempDeleted: false }).ToList();
        var deleted = products.Where(p => p is { TempAdded: false, TempDeleted: true }).ToList();
        var both = products.Where(p => p is { TempAdded: true, TempDeleted: true }).ToList();
        return new Tuple<List<CartItem>, List<CartItem>, List<CartItem>>(added, deleted, both);
    }
}