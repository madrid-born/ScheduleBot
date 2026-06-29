using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class CartService(AppDbContext dbContext) : DatabaseService(dbContext)
{
    public List<Cart> GetCartsByTelId(long chatId)
    {
        throw new NotImplementedException();
    }
}