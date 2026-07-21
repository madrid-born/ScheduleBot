using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class CartService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<Cart?> GetCartByCartId(Guid cartId)
    {
        return await _dbContext.Cart.FirstOrDefaultAsync(c => c.Id == cartId);
    }
    
    public async Task<List<CartItem>> GetProductsByCartId(Guid cartId)
    {
        return await _dbContext.CartItem.Where(c => c.CartId == cartId).ToListAsync();
    }
    
    public async Task<List<Cart>> GetCartsByTelId(long chatId = 0)
    {
        var user = await GetUserByTelId(chatId);
        var cartAccesses = await _dbContext.CartAccess.Where(c => c.UserId == user!.Id).Select(c => c.CartId).ToListAsync();
        return await _dbContext.Cart.Where(c => cartAccesses.Contains(c.Id)).ToListAsync();
    }
    
    public async Task<List<Tuple<string, List<string?>>>> GetCartAndItemsByCartId(Guid cartId)
    {
        var cart = await GetCartByCartId(cartId);
        var cartItems = (await GetProductsByCartId(cartId)).Select(x => x.Name).ToList();
        return [new(cart!.Name!, cartItems)];
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
        
        var result = await _dbContext.Cart
            .Where(c => c.Id == cartId)
            .ExecuteDeleteAsync();
        return result > 0;
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
        };
        
        _dbContext.CartItem.Add(cartItem);
        return await _dbContext.SaveChangesAsync() > 0;
    }
    
    public async Task<bool> RemoveProductFromCart(Guid cartId, string productName)
    {
        var cart = await GetCartByCartId(cartId);
        var result = await _dbContext.CartItem
            .Where(c => c.CartId == cart!.Id && c.Name == productName)
            .ExecuteDeleteAsync();
        return result > 0;
    }
}