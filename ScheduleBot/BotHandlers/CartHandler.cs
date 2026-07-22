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
                await ProductAction(data, data.DataSeparated[2]);
                break;
            }
        }
    }
    
    private async Task LoadCarts(long chatId, string callBack, int pageNumber = 0)
    {
        var carts = await cServices.GetCartsByTelId(chatId);
        if (callBack == CallBacks.DeleteCart)
        {
            var user = await cServices.GetUserByTelId(chatId);
            carts = carts.Where(c => c.CreatorId == user!.Id).ToList();
        }
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
        var carts = await cServices.GetCartsByTelId(data.ChatId);
        carts = carts.Where(c => cartId == Guid.Empty || c.Id == cartId).ToList();
        foreach (var cart in carts)
        {
            var cartDetail = await cServices.GetCartAndItemsByCartId2(cart.Id);
            await ShowCart(data.ChatId, cartDetail);
        }
    }

    private async Task ShowCart(long chatId, Tuple<Cart, List<CartItem?>> cart)
    {
        if (cart.Item2.Count == 0)
        {
            await services.SendMessage(chatId, string.Format(Messages.CartEmpty, cart.Item1.Name));
            return;
        }
        var items = string.Join("\n", cart.Item2.Select(x => x!.Name));
        var message = string.Format(Messages.ShowCart, cart.Item1.Name, items);
        await services.SendMessage(chatId, message);
    }
    
    public async Task CreateCart(UpdateData data)
    {
        var cartName = data.MessageText!;
        var cartId = await cServices.CreateNewCart(data.ChatId, cartName);
        await services.SendMessage(data.ChatId, string.Format(Messages.CartCreated, cartName, cartId));
    }

    private async Task DeleteCart(UpdateData data, Guid cartId)
    {
        var usersWithAccess = await cServices.GetUsersWithAccessByCartId(cartId);
        var cart = await cServices.GetCartAndItemsByCartId2(cartId);
        var changer = await cServices.GetUserByTelId(data.ChatId);
        var isDeleted = await cServices.DeleteCart(data.ChatId, cartId);
        if (isDeleted)
        {
            foreach (var user in usersWithAccess)
            {
                await services.SendMessage(user.ChatId, string.Format(Messages.CartDeleted, cart.Item1.Name, changer!.FullName));
                await ShowCart(user.ChatId, cart);
            }
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
                await services.SendMessage(data.ChatId, string.Format(Messages.InviteAccepted, cart.Name));
                var creator = await cServices.GetUserById(cart.CreatorId);
                var joiner = await cServices.GetUserByTelId(data.ChatId);
                await services.SendMessage(creator!.ChatId, string.Format(Messages.InviteAcceptedOwner, joiner!.FullName, cart.Name));
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

    private ReplyMarkup CreateProductKeyboard(List<CartItem> products, List<CartItem>? newOnes = null, List<CartItem>? oldOnes = null)
    {
        List<List<Tuple<string, string>>> collection = [];
        for (var index = 0; index < (double)products.Count/3 ; index += 1)
        {
            List<Tuple<string, string>> row = [];
            for (var i = 0; i < 3; i++)
            {
                var product = new CartItem { Id = Guid.Empty, Name = "-" };
                try { product = products[index*3 + i]; }
                catch (Exception e) { /*ignored*/ }

                var prefix = "";
                if (product.TempAdded) prefix = "🆕 ";
                if (product.TempDeleted) prefix = "🗑 ";
                
                row.Add(new Tuple<string, string>(prefix+product.Name!, $"{product.Id.ToString()}"));
            }
            collection.Add(row);
        }
        collection.Add([new (Messages.Done, $"{CallBacks.Done}"), new (Messages.Cancel, $"{CallBacks.Cancel}"),]);
        
        return services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Cart}\\{CallBacks.ProductAction}\\");
    }
    
    private async Task EditProductKeyboard(long chatId, int messageId, Guid cartId)
    {
        var products = await cServices.GetProductsByCartId(cartId);
        var keyboard = CreateProductKeyboard(products);
        await bot.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: messageId,
            replyMarkup: (InlineKeyboardMarkup) keyboard
        );
    }
    
    private async Task LoadProducts(UpdateData data, string cartIdAsString)
    {
        var isLoaded = Guid.TryParse(cartIdAsString, out var cartId);
        if (!isLoaded) await services.SendMessage(data.ChatId, Messages.CartLoadFail);
        var products = await cServices.GetProductsByCartId(cartId);
        var keyboard = CreateProductKeyboard(products);
        var messageId = await services.SendMessage(data.ChatId, Messages.ProductAction, replyMarkup: keyboard);
        sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingProductActions,
            callbackData: $"{messageId}\\{cartIdAsString}");
    }

    public async Task AddProductToCart(UpdateData data, string? callbackData)
    {
        var productName = data.MessageText!;
        if (callbackData == null)
        {
            await services.SendMessage(data.ChatId, Messages.CartNotFound);
            return;
        }
        var callbacks = callbackData.Split("\\").ToList();
        var isMessageId = int.TryParse(callbacks[0], out var messageId);
        var isCartId = Guid.TryParse(callbacks[1], out var cartId);
        if (!isCartId || !isMessageId) await services.SendMessage(data.ChatId, Messages.CartIdFormatFail);
        var appended = await cServices.AddProductToCart(cartId, productName);
        await EditProductKeyboard(data.ChatId, messageId, cartId);
    }
    
    private async Task ProductAction(UpdateData data, string callBack)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session == null) throw new Exception();
        var callbacks = session.CallbackData.Split("\\").ToList();
        var isMessageId = int.TryParse(callbacks[0], out var messageId);
        var isCartId = Guid.TryParse(callbacks[1], out var cartId);
        var cart = await cServices.GetCartByCartId(cartId);
        if (!isMessageId || !isCartId || cart == null) throw new Exception();

        switch (callBack)
        {
            case CallBacks.Done:
            {
                var changes = CreateChangeMessage(await cServices.LoadProductServiceChanges(cartId));
                var changer = await cServices.GetUserByTelId(data.ChatId);
                var submitted = await cServices.SubmitProductServiceChanges(cartId);
                if (!submitted) throw new Exception();
                var message = string.Format(Messages.ProductActionSubmitted, cart.Name, changer!.FullName, changes);
                var usersWithAccess = await cServices.GetUsersWithAccessByCartId(cartId);
                sessionService.ClearSession(data.ChatId);
                await bot.DeleteMessage(data.ChatId, messageId);
                foreach (var user in usersWithAccess) await services.SendMessage(user.ChatId, message);
                break;
            }
            case CallBacks.Cancel:
            {
                var changes = CreateChangeMessage(await cServices.LoadProductServiceChanges(cartId));
                var canceled = await cServices.CancelProductServiceChanges(cartId);
                if (!canceled) throw new Exception();
                var message = string.Format(Messages.ProductActionAborted, cart.Name, changes);
                sessionService.ClearSession(data.ChatId);
                await bot.DeleteMessage(data.ChatId, messageId);
                await services.SendMessage(data.ChatId, message);
                break;
            }
            default:
            {
                var tryParse = Guid.TryParse(callBack, out var productId);
                if (!tryParse) throw new Exception();
                var deleted = await cServices.DeleteProductFromCart(productId);
                if (!deleted) throw new Exception();
                await EditProductKeyboard(data.ChatId, messageId, cartId);
                break;
            }
        }
    }

    private static string CreateChangeMessage(Tuple<List<CartItem>, List<CartItem>, List<CartItem>> changes)
    {
        var added = string.Join("\n", changes.Item1.Select(x => x.Name));
        var deleted = string.Join("\n", changes.Item2.Select(x => x.Name));
        var both = string.Join("\n", changes.Item3.Select(x => x.Name));
        return string.Format(Messages.ProductActionChanges, added, deleted, both);
    }
    
    #endregion
    
}