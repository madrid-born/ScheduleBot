using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;

namespace ScheduleBot.BotHandlers;

public class CartHandler(ITelegramBotClient bot, IServiceProvider serviceProvider,
    MainService services, CartService cServices, ILogger<CycleTrackerHandler> logger)
{
    
    #region Handel

    public async Task HandleSection(UpdateData data, bool? productService = null)
    {
        List<List<Tuple<string, string>>> collection;
        
        if (productService == null)
        {
            collection = 
            [
                [new(Messages.KeyboardProduct, CallBacks.ProductService), new(Messages.KeyboardCart, CallBacks.CartService)],
                [new(Messages.KeyboardShow,    CallBacks.Show)],
            ];
        }
        else if ((bool)productService)
        {
            collection = 
            [
                [
                    new(Messages.KeyboardAddProduct,    CallBacks.AddProduct),
                    new(Messages.KeyboardRemoveProduct, CallBacks.RemoveProduct)
                ],
            ];
        }
        else
        {
            collection = 
            [
                [new(Messages.KeyboardCreateCart, CallBacks.CreateCart), new(Messages.KeyboardInviteToCart, CallBacks.InviteToCart)],
                [new(Messages.KeyboardResetCart,  CallBacks.ResetCart),  new(Messages.KeyboardDeleteCart, CallBacks.DeleteCart)],
            ];
        }
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cart}\\{CallBacks.MainSection}\\");
        await services.SendMessage(data.ChatId, Messages.LoadCart, replyMarkup: keyboard);
    }
    
    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case CallBacks.MainSection:
                switch (data.DataSeparated[2])
                {
                    case CallBacks.ProductService:
                        await HandleSection(data, true);
                        break;
                    case CallBacks.CartService:
                        await HandleSection(data, false);
                        break;
                    case CallBacks.CreateCart:
                        //todo
                        break;
                    case CallBacks.Show:
                    case CallBacks.AddProduct:
                    case CallBacks.RemoveProduct:
                    case CallBacks.InviteToCart:
                    case CallBacks.DeleteCart:
                    case CallBacks.ResetCart:
                        await LoadCarts(data.ChatId, data.DataSeparated[2]);
                        break;
                }
                break;
            case CallBacks.Show:
                break;
            case CallBacks.AddProduct:
                break;
            case CallBacks.RemoveProduct:
                break;
            case CallBacks.InviteToCart:
                break;
            case CallBacks.DeleteCart:
                break;
            case CallBacks.ResetCart:
                break;
        }
    }

    private async Task LoadCarts(long chatId, string callBack, int pageNumber = 0)
    {
        List<Cart> carts = cServices.GetCartsByTelId(chatId);
        List<List<Tuple<string, string>>> collection = [];
        for (var index = pageNumber * 4; index < pageNumber * 4 + 4; index += 2)
        {
            List<Tuple<string, string>> row = [];
            for (var i = 0; i < 2; i++)
            {
                var cart = new Cart { Id = Guid.Empty, Name = "-" };
                try { cart = carts[index + i]; }
                catch (Exception e) { /*ignored*/ }
                
                row.Add(new Tuple<string, string>(cart.Name!, $"{callBack}\\{cart.Id.ToString()}"));
            }
            collection.Add(row);
        }
        collection.Add(
        [
            new (Messages.PreviousPage, $"{CallBacks.PreviousPage}\\{callBack}\\{pageNumber}"),
            new (pageNumber.ToString(), ""),
            new (Messages.NextPage,     $"{CallBacks.NextPage}\\{callBack}\\{pageNumber}")

        ]);
        if (new List<string>{CallBacks.Show}.Contains(callBack))
        {
            collection.Add([new (Messages.All,CallBacks.All)]);
        }
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cart}\\");
        await services.SendMessage(chatId, Messages.SelectCart, replyMarkup: keyboard);
    }

    #endregion
}