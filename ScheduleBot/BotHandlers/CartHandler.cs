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
                    case CallBacks.Show:
                        await ShowCart(data);
                        break;
                    case CallBacks.AddProduct:
                        //await 
                        break;
                    case CallBacks.RemoveProduct:
                        //await 
                        break;
                    case CallBacks.CreateCart:
                        //await 
                        break;
                    case CallBacks.InviteToCart:
                        //await 
                        break;
                    case CallBacks.DeleteCart:
                        //await 
                        break;
                    case CallBacks.ResetCart:
                        //await 
                        break;
                }
                break;
        }
    }

    private async Task ShowCart(UpdateData data)
    {
        throw new NotImplementedException();
    }

    private async Task LoadCart()
    {
        List<Cart> carts;

    }
    
    // private async Task ShowNotifyModeMenu(long chatId)
    // {
    //     
    //     var collection = new List<List<Tuple<string, string>>>();
    //     collection.AddRange(Messages.NotifyModes.Select((notifyMode, index) =>
    //         (List<Tuple<string, string>>)[new(notifyMode, $"{index}\\{cycleId}")]));
    //
    //     var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.SetNotifyMode}\\");
    //     await services.SendMessage(chatId, Messages.AskForNotifyMode, replyMarkup: keyboard);
    // }

    #endregion
}