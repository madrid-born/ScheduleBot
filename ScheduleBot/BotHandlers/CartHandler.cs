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
                    case CallBacks.ProductService:
                        await HandleSection(data, true);
                        break;
                    case CallBacks.CartService:
                        await HandleSection(data, false);
                        break;
                    case CallBacks.CreateCart:
                        await services.SendMessage(data.ChatId, Messages.AskCartName, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.JoinToCart:
                        await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.Show:
                    case CallBacks.InviteToCart:
                    case CallBacks.DeleteCart:
                    case CallBacks.AddProduct:
                    case CallBacks.RemoveProduct:
                        await LoadCarts(data.ChatId, data.DataSeparated[2]);
                        break;
                    case CallBacks.ProductAction:
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
            case CallBacks.ProductAction:
            {
                await LoadProducts(data, data.DataSeparated[2]);
                break;
            }
            case CallBacks.AddProduct:
            {
                await AskProductName(data, Guid.Parse(data.DataSeparated[2]));
                break;
            }
            case CallBacks.RemoveProduct:
            {
                await LoadRemoveProduct(data, Guid.Parse(data.DataSeparated[2]));
                break;
            }
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
        var carts = await cServices.GetCartsDetailByTelId(data.ChatId, cartId);
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
        var cart = (await cServices.GetCartsDetailByTelId(cartId: cartId)).First();
        var isDeleted = await cServices.DeleteCart(data.ChatId, cartId);
        if (isDeleted)
        {
            await services.SendMessage(data.ChatId, string.Format(Messages.CartDeleted, cart.Item1));
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
            await services.SendMessage(data.ChatId, Messages.CartIdFormatFail);
            await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
        }
        else
        {
            var cart = await cServices.GetCartByCartId(cartId);
            if (cart == null)
            {
                await services.SendMessage(data.ChatId, Messages.CartNotExist);
                await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
            }

            await cServices.InviteAccept(data.ChatId, cartId);
            await services.SendMessage(data.ChatId, string.Format(Messages.InviteAccepted, cart!.Name));
        }
    }
    
    #endregion

    #region ProductMethods
    
    private async Task LoadProducts(UpdateData data, string cartIdAsString)
    {
        var isLoaded = Guid.TryParse(cartIdAsString, out var cartId);
        if (!isLoaded) await services.SendMessage(data.ChatId, Messages.CartLoadFail);
        sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingProductActions, callbackData: cartIdAsString);
        List<CartItem> products = cServices.getProductsByCartId(cartId);
        //TODO here
    }
    
    
    
    
    
    
    
    private async Task AskProductName(UpdateData data, Guid cartId)
    {
        var cart = await cServices.GetCartByCartId(cartId);
        if (cart != null) await services.SendMessage(data.ChatId, string.Format(Messages.InviteToCart, cart.Name, cart.Id));
        sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingProductActions, callbackData: cartId.ToString());

        throw new NotImplementedException();
    }

    public async Task AddProductToCart(UpdateData data, string? cartIdAsString)
    {
        var productName = data.MessageText!;
        // var cartIdAsString = sessionService.GetData(data.ChatId, Actions.AwaitingProductName);
        if (cartIdAsString == null) await services.SendMessage(data.ChatId, Messages.CartNotFound);
        var isCartId = Guid.TryParse(cartIdAsString, out var cartId);
        if (isCartId) await services.SendMessage(data.ChatId, Messages.CartIdFormatFail);
        var appended = await cServices.AddProductToCart(data.ChatId, cartId, productName);
        // await services.SendMessage(data.ChatId, string.Format(Messages.CartCreated, cartName, cartId));
    }
    
    private async Task AddProductToCart(UpdateData data)
    {
        var productName = data.MessageText!;
        var cartIdAsString = sessionService.GetData(data.ChatId, Actions.AwaitingProductActions);
        if (cartIdAsString == null) await services.SendMessage(data.ChatId, Messages.CartNotFound);
        var isCartId = Guid.TryParse(cartIdAsString, out var cartId);
        if (isCartId) await services.SendMessage(data.ChatId, Messages.CartIdFormatFail);
        var appended = await cServices.AddProductToCart(data.ChatId, cartId, productName);
        // await services.SendMessage(data.ChatId, string.Format(Messages.CartCreated, cartName, cartId));
    }


    private async Task LoadRemoveProduct(UpdateData data, Guid cartId)
    {
        throw new NotImplementedException();
    }

    #endregion
    
}