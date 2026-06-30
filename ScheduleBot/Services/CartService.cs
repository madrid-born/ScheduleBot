using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class CartService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    public async Task<Cart?> GetCartByCartId(Guid cartId)
    {
        throw new NotImplementedException();
    }
    
    public async Task<List<Cart>> GetCartsByTelId(long chatId = 0, Guid cartId = default)
    {
        throw new NotImplementedException();
    }
    
    public async Task<List<Tuple<string, List<string>>>> GetCartsDetailByTelId(long chatId = 0, Guid cartId = default)
    {
        var carts = await GetCartsByTelId(chatId, cartId);
        throw new NotImplementedException();
    }
    
    public async Task<Guid> CreateNewCart(long dataChatId, string cartName)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> DeleteCart(long dataChatId, Guid cartId)
    {
        throw new NotImplementedException();
    }

    public async Task InviteAccept(long dataChatId, Guid cartId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> AddProductToCart(long dataChatId, Guid cartId, string productName)
    {
        throw new NotImplementedException();
    }
    public async Task<bool> RemoveProductFromCart(long dataChatId, Guid cartId, string productName)
    {
        throw new NotImplementedException();
    }

    public List<CartItem> getProductsByCartId(Guid cartId)
    {
        throw new NotImplementedException();
    }
}