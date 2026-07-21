using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class CartHandler(ITelegramBotClient bot, IServiceProvider serviceProvider, UserSessionService sessionService,
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
                [new(Messages.KeyboardCreateCart, CallBacks.CreateCart), new(Messages.KeyboardDeleteCart, CallBacks.DeleteCart)],
                [new(Messages.KeyboardJoinToCart,  CallBacks.JoinToCart),  new(Messages.KeyboardInviteToCart, CallBacks.InviteToCart)],
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
                    // case CallBacks.ProductService:
                        // await HandleSection(data, true);
                        // break;
                    case CallBacks.CartService:
                        await HandleSection(data, false);
                        break;
                    case CallBacks.CreateCart:
                        await services.SendMessage(data.ChatId, Messages.AskCartName, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.JoinToCart:
                        await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.ProductService:
                    case CallBacks.Show:
                    case CallBacks.InviteToCart:
                    case CallBacks.DeleteCart:
                    // case CallBacks.AddProduct:
                    // case CallBacks.RemoveProduct:
                        await LoadCarts(data.ChatId, data.DataSeparated[2]);
                        break;
                }
                break;
            case CallBacks.Show:
            {
                var tryParse = Guid.TryParse(data.DataSeparated[2], out var cartId2);
                await ShowCarts(data, tryParse ? cartId2 : Guid.Empty);
                break;
            }
            case CallBacks.ProductService:
            {
                await LoadProducts(data, data.DataSeparated[2]);
                break;
            }
            // case CallBacks.AddProduct:
            // {
            //     await AskProductName(data, Guid.Parse(data.DataSeparated[2]));
            //     break;
            // }
            // case CallBacks.RemoveProduct:
            // {
            //     await LoadRemoveProduct(data, Guid.Parse(data.DataSeparated[2]));
            //     break;
            // }
            case CallBacks.DeleteCart:
            {
                await DeleteCart(data, Guid.Parse(data.DataSeparated[2]));
                break;
            }
            case CallBacks.InviteToCart:
            {
                await InviteToCart(data, Guid.Parse(data.DataSeparated[2]));
                break;
            }
            case CallBacks.PreviousPage:
            {
                var callBack = data.DataSeparated[2];
                var pageNumber = int.Parse(data.DataSeparated[3]) - 1;
                await  LoadCarts(data.ChatId, callBack, pageNumber);
                break;
            }
            case CallBacks.NextPage:
            {
                var callBack = data.DataSeparated[2];
                var pageNumber = int.Parse(data.DataSeparated[3]) + 1;
                await  LoadCarts(data.ChatId, callBack, pageNumber);
                break;
            }
            case CallBacks.ProductAction:
            {
                await ProductAction(data);
                break;
            }
        }
    }
    
    private async Task LoadCarts(long chatId, string callBack, int pageNumber = 0)
    {
        var carts = await cServices.GetCartsByTelId(chatId);
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
            collection.Add([new (Messages.All, $"{callBack}\\{CallBacks.All}")]);
        }
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cart}\\");
        await services.SendMessage(chatId, Messages.SelectCart, replyMarkup: keyboard);
    }
    
    #endregion

    #region CartMethods
    
    private async Task ShowCarts(UpdateData data, Guid cartId = default)
    {
        var carts = await cServices.GetCartAndItemsByCartId(cartId);
        foreach (var cart in carts)
        {
            await ShowCart(data, cart);
        }
    }

    private async Task ShowCart(UpdateData data, Tuple<string,List<string>> cart)
    {
        var items = string.Join("\n", cart.Item2);
        var message = string.Format(Messages.ShowCart, cart.Item1, items);
        await services.SendMessage(data.ChatId, message, addMainKeyboard: true);
    }
    
    public async Task CreateCart(UpdateData data)
    {
        var cartName = data.MessageText!;
        var cartId = await cServices.CreateNewCart(data.ChatId, cartName);
        await services.SendMessage(data.ChatId, string.Format(Messages.CartCreated, cartName, cartId));
    }

    private async Task DeleteCart(UpdateData data, Guid cartId)
    {
        var cart = (await cServices.GetCartAndItemsByCartId(cartId)).First();
        var isDeleted = await cServices.DeleteCart(data.ChatId, cartId);
        if (isDeleted)
        {
            await services.SendMessage(data.ChatId, string.Format(Messages.CartDeleted, cart.Item1));
            //todo : send to all
            await ShowCart(data, cart);
        }
        else await services.SendMessage(data.ChatId, Messages.CartDeleteFail);
    }

    private async Task InviteToCart(UpdateData data, Guid cartId)
    {
        var cart = await cServices.GetCartByCartId(cartId);
        if (cart != null) await services.SendMessage(data.ChatId, string.Format(Messages.InviteToCart, cart.Name, cart.Id));
        else await services.SendMessage(data.ChatId, Messages.CartNotFound);
    }

    public async Task JoinToCart(UpdateData data)
    {
        var isCartId = Guid.TryParse(data.MessageText!, out var cartId);
        if (isCartId)
        {
            var cart = await cServices.GetCartByCartId(cartId);
            if (cart != null)
            {
                await cServices.InviteAccept(data.ChatId, cartId);
                await services.SendMessage(data.ChatId, string.Format(Messages.InviteAccepted, cart!.Name));
            }
            else
            {
                await services.SendMessage(data.ChatId, Messages.CartNotExist);
                await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
            }
        }
        else
        {
            await services.SendMessage(data.ChatId, Messages.CartIdFormatFail);
            await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
        }
    }
    
    #endregion

    #region ProductMethods
    
    private async Task LoadProducts(UpdateData data, string cartIdAsString)
    {
        var isLoaded = Guid.TryParse(cartIdAsString, out var cartId);
        if (!isLoaded) await services.SendMessage(data.ChatId, Messages.CartLoadFail);
        sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingProductActions, callbackData: cartIdAsString);
        var session = sessionService.GetPendingAction(data.ChatId);

        var products = await cServices.GetProductsByCartId(cartId);
        
        List<List<Tuple<string, string>>> collection = [];
        for (var index = 0; index < products.Count/3 ; index += 1)
        {
            List<Tuple<string, string>> row = [];
            for (var i = 0; i < 3; i++)
            {
                var product = new CartItem { Id = Guid.Empty, Name = "-" };
                try { product = products[index*3 + i]; }
                catch (Exception e) { /*ignored*/ }
                
                row.Add(new Tuple<string, string>(product.Name!, $"{product.Id.ToString()}"));
            }
            collection.Add(row);
        }
        collection.Add(
        [
            new (Messages.Done, $"{CallBacks.Done}"),
            // new (pageNumber.ToString(), ""),
        ]);
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cart}\\{CallBacks.ProductAction}\\");
        await services.SendMessage(data.ChatId, Messages.ProductAction, replyMarkup: keyboard);
    }
    
    public async Task AddProductToCart(UpdateData data, string? cartIdAsString)
    {
        var productName = data.MessageText!;
        if (cartIdAsString == null) await services.SendMessage(data.ChatId, Messages.CartNotFound);
        var isCartId = Guid.TryParse(cartIdAsString, out var cartId);
        if (isCartId) await services.SendMessage(data.ChatId, Messages.CartIdFormatFail);
        var appended = await cServices.AddProductToCart(cartId, productName);
        
        // await services.SendMessage(data.ChatId, string.Format(Messages.CartCreated, cartName, cartId));
    }

    
    private async Task ProductAction(UpdateData data)
    {
        var ss2 = Guid.Parse(data.DataSeparated[2]);
        var ss3 = Guid.Parse(data.DataSeparated[3]);
        throw new NotImplementedException();
    }
    
    // private async Task AskProductName(UpdateData data, Guid cartId)
    // {
    //     var cart = await cServices.GetCartByCartId(cartId);
    //     if (cart != null) await services.SendMessage(data.ChatId, string.Format(Messages.InviteToCart, cart.Name, cart.Id));
    //     sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingProductActions, callbackData: cartId.ToString());
    //
    //     throw new NotImplementedException();
    // }
    //
    //
    // private async Task LoadRemoveProduct(UpdateData data, Guid cartId)
    // {
    //     throw new NotImplementedException();
    // }

    #endregion
    
}