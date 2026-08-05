using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Word;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class TransactionHandler(ITelegramBotClient bot, IServiceProvider serviceProvider, IConfiguration configuration,
    UserSessionService sessionService, MainService services, TransactionService tServices, ILogger<CycleTrackerHandler> logger)
{
    #region Handel

    public async Task HandleSection(UpdateData data)
    {
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.KeyboardWalletManagement,   CallBacks.WalletManagement)],
            [new(Messages.KeyboardCreateWallet,       CallBacks.CreateWallet)]
        ];
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}|{CallBacks.MainSection}|");
        await services.SendMessage(data.ChatId, Messages.LoadTransaction, replyMarkup: keyboard);
    }

    public async Task HandleCallBack(UpdateData data)
    {
        var action = data.DataSeparated.ElementAtOrDefault(1);
        var value = data.DataSeparated.ElementAtOrDefault(2);
        switch (action)
        {
            case CallBacks.MainSection:
                switch (value)
                {
                    case CallBacks.CreateWallet:
                        await services.SendMessage(data.ChatId, Messages.AskWalletName, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.WalletManagement:
                        await LoadWallets(data.ChatId, CallBacks.WalletManagement);
                        break;
                }
                break;
            case CallBacks.WalletManagement when Guid.TryParse(value, out var walletId):
                await WalletMenu(data, walletId);
                break;
            case CallBacks.InviteToWallet when Guid.TryParse(value, out var walletId):
                await InviteWallet(data.ChatId, walletId);
                break;
            case CallBacks.ManageCategories:
                await LoadCategories(data, value!);
                break;
            case CallBacks.CategoryAction:
                await CategoryAction(data, value);
                break;
            
            case CallBacks.AddTransaction when Guid.TryParse(value, out var walletId):
                await AddTransactionMenu(data.ChatId, walletId);
                break;
            case CallBacks.ManualTransaction when Guid.TryParse(value, out var walletId):
                throw new NotImplementedException();
            case CallBacks.BluTransaction when Guid.TryParse(value, out var walletId):
                sessionService.SetData(data.ChatId, Actions.AwaitingBluFile, walletId.ToString());
                await services.SendMessage(data.ChatId, Messages.BluFilePrompt);
                break;
            case CallBacks.BluAction:
                await BluAction(data);
                break;
        }
    }
    
    private async Task LoadWallets(long chatId, string callBack, int pageNumber = 0)
    {
        var wallets = await tServices.GetWalletsByTelId(chatId);
        if (callBack == CallBacks.DeleteWallet)
        {
            var user = await tServices.GetUserByTelId(chatId);
            wallets = wallets.Where(c => c.CreatorId == user!.Id).ToList();
        }
        var collection = services.LoadCollectionInPages(wallets, callBack, pageNumber, x => x.Id, x => x.Name!);
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}|");
        await services.SendMessage(chatId, Messages.SelectWallet, replyMarkup: keyboard);
    }
    
    #endregion

    #region WalletManagement
    
    public async Task CreateWallet(UpdateData data)
    {
        var walletName = data.MessageText!;
        var walletId = await tServices.CreateNewWallet(data.ChatId, walletName);
        await services.SendMessage(data.ChatId, string.Format(Messages.WalletCreated, walletName, walletId));
    }
    
    private async Task WalletMenu(UpdateData data, Guid walletId)
    {
        var wallet = await tServices.GetWalletForUser(walletId, data.ChatId);
        if (wallet == null) { await services.SendMessage(data.ChatId, Messages.WalletNotFound); return; }
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.KeyboardInviteToWallet,   $"{CallBacks.InviteToWallet}|{walletId}")],
            [new(Messages.KeyboardManageCategories, $"{CallBacks.ManageCategories}|{walletId}")],
            [new(Messages.KeyboardAddTransaction,   $"{CallBacks.AddTransaction}|{walletId}")]
        ];

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}|");
        await services.SendMessage(data.ChatId, string.Format(Messages.WalletSelected, wallet.Name), replyMarkup: keyboard);
    }
    
    private async Task InviteWallet(long chatId, Guid walletId)
    {
        var wallet = await tServices.GetWalletForUser(walletId, chatId);
        if (wallet == null)
        {
            await services.SendMessage(chatId, Messages.WalletNotFound);
            return;
        }
        var link = $"{services.Url}?start={CallBacks.Transaction}_{CallBacks.JoinWallet}_{walletId}";
        var keyboard =  InlineKeyboardButton.WithUrl("Direct join to the Wallet", link);
        await services.SendMessage(chatId, string.Format(Messages.InviteToWallet, wallet.Name), replyMarkup: keyboard);
    }
    
    public async Task JoinToWalletById(UpdateData data)
    {
        var idAvailable = Guid.TryParse(data.MessageText, out var walletId);
        var wallet = await tServices.GetWalletByWalletId(walletId);
        if (idAvailable && await tServices.InviteAccept(data.ChatId, walletId))
            await services.SendMessage(data.ChatId, string.Format(Messages.WalletJoined, wallet!.Name), true);
        else
            await services.SendMessage(data.ChatId, Messages.CycleIdIsWrong, true);
    }
    
    #endregion

    #region Category
    
    private ReplyMarkup CreateCategoriesKeyboard(List<Category> categories)
    {
        var collection = services.LoadCollectionInScroller(categories, x => x.Id, x => x.Name!, x => x.TempAdded, x => x.TempDeleted);
        return services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Transaction}|{CallBacks.CategoryAction}|");
    }

    private async Task LoadCategories(UpdateData data, string walletIdAsString)
    {
        var isLoaded = Guid.TryParse(walletIdAsString, out var walletId);
        if (!isLoaded) await services.SendMessage(data.ChatId, Messages.WalletLoadFail);
        var categories = await tServices.GetCategoriesByWalletId(walletId);
        var keyboard = CreateCategoriesKeyboard(categories);
        var messageId = await services.SendMessage(data.ChatId, Messages.ScrollerAction, replyMarkup: keyboard);
        sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingCategoryName, callbackData: $"{messageId}|{walletIdAsString}");
    }
    
    private async Task EditCategoriesKeyboard(long chatId, int messageId, Guid walletId)
    {
        var categories = await tServices.GetCategoriesByWalletId(walletId);
        var keyboard = CreateCategoriesKeyboard(categories);
        await bot.EditMessageReplyMarkup(chatId: chatId, messageId: messageId, replyMarkup: (InlineKeyboardMarkup) keyboard);
    }
    
    public async Task AddCategoriesToWallet(UpdateData data, string? callbackData)
    {
        var categoryName = data.MessageText!;
        if (callbackData == null)
        {
            await services.SendMessage(data.ChatId, Messages.WalletNotFound);
            return;
        }
        var callbacks = callbackData.Split("|").ToList();
        var isMessageId = int.TryParse(callbacks[0], out var messageId);
        var isWalletId = Guid.TryParse(callbacks[1], out var walletId);
        if (!isWalletId || !isMessageId) await services.SendMessage(data.ChatId, Messages.WalletIdFormatFail);
        var appended = await tServices.AddCategoryToWallet(walletId, categoryName);
        await EditCategoriesKeyboard(data.ChatId, messageId, walletId);
    }
    
    private async Task CategoryAction(UpdateData data, string callBack)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session == null) throw new Exception();
        var callbacks = session.CallbackData.Split("|").ToList();
        var isMessageId = int.TryParse(callbacks[0], out var messageId);
        var isWalletId = Guid.TryParse(callbacks[1], out var walletId);
        var wallet = await tServices.GetWalletByWalletId(walletId);
        if (!isMessageId || !isWalletId || wallet == null) throw new Exception();

        switch (callBack)
        {
            case CallBacks.Done:
            {
                var changes = CreateChangeMessage(await tServices.LoadCategoryServiceChanges(walletId));
                var changer = await tServices.GetUserByTelId(data.ChatId);
                var submitted = await tServices.SubmitCategoryServiceChanges(walletId);
                if (!submitted) throw new Exception();
                var message = string.Format(Messages.ScrollerActionSubmitted, wallet.Name, changer!.FullName, changes, CallBacks.Transaction);
                var usersWithAccess = await tServices.GetUsersWithAccessByWalletId(walletId);
                sessionService.ClearSession(data.ChatId);
                await bot.DeleteMessage(data.ChatId, messageId);
                foreach (var user in usersWithAccess) await services.SendMessage(user.ChatId, message);
                break;
            }
            case CallBacks.Cancel:
            {
                var changes = CreateChangeMessage(await tServices.LoadCategoryServiceChanges(walletId));
                var canceled = await tServices.CancelCategoryServiceChanges(walletId);
                if (!canceled) throw new Exception();
                var message = string.Format(Messages.ScrollerActionAborted, wallet.Name, changes, CallBacks.Transaction);
                sessionService.ClearSession(data.ChatId);
                await bot.DeleteMessage(data.ChatId, messageId);
                await services.SendMessage(data.ChatId, message);
                break;
            }
            default:
            {
                var tryParse = Guid.TryParse(callBack, out var categoryId);
                if (!tryParse) throw new Exception();
                var deleted = await tServices.DeleteCategoryFromWallet(categoryId);
                if (!deleted) throw new Exception();
                await EditCategoriesKeyboard(data.ChatId, messageId, walletId);
                break;
            }
        }
    }
    
    private static string CreateChangeMessage(Tuple<List<string>, List<string>, List<string>> changes)
    {
        var added = string.Join("\n", changes.Item1);
        var deleted = string.Join("\n", changes.Item2);
        var both = string.Join("\n", changes.Item3);
        return string.Format(Messages.ScrollerActionChanges, added, deleted, both);
    }
    
    #endregion

    #region Transaction


    private async Task AddTransactionMenu(long chatId, Guid walletId)
    {
        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.KeyboardManualTransaction, $"{CallBacks.ManualTransaction}|{walletId}")],
            [new(Messages.KeyboardBluTransaction, $"{CallBacks.BluTransaction}|{walletId}")]
        ];
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}|");
        await services.SendMessage(chatId, Messages.KeyboardAddTransaction, replyMarkup: keyboard);
    }

    public async Task ProcessBluFile(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        var transactionProcesses = new List<TransactionProcess>();
        using var workbook = new XLWorkbook(data.Document!.FileAddress);
        var ws = workbook.Worksheet(1);
        for (var row = 12; !ws.Cell(row, 19).IsEmpty(); row++)
            transactionProcesses.Add(new TransactionProcess
            {
                Index = ws.Cell(row, 19).GetValue<int>(),
                Date = MainService.ConvertJalaliToGregorian(ws.Cell(row, 18).GetString()),
                Type = ws.Cell(row, 7).GetString(),
                Description = ws.Cell(row, 11).GetString(),
                DocumentNo = long.Parse(ws.Cell(row, 16).GetString()),
                Deposit = ws.Cell(row, 4).GetValue<decimal>()/10,
                Withdraw = ws.Cell(row, 3).GetValue<decimal>()/10,
                BalanceAfter = ws.Cell(row, 2).GetValue<decimal>()/10,
                Processed = false
            });
        transactionProcesses.Reverse();
        session.SetAction(Actions.AwaitingBluReview);
        session.SetContext(Context.Tps, transactionProcesses);
        session.SetContext(Context.Wallet, session.CallbackData);
        session.SetContext(Context.Index, 0);
        session.SetContext(Context.MessageId, 0);
        session.SetCallBack(CallBacks.WaitForReview);
        await ShowBluRow(data.ChatId);
    }
    
    private async Task ShowBluRow(long chatId)
    {
        var session = sessionService.GetData(chatId);
        var transactionProcesses = (List<TransactionProcess>)session!.Context[Context.Tps];
        var index = (int)session.Context[Context.Index];
        var walletId = (Guid)session.Context[Context.Wallet];
        var loadedMessageId = (int)session.Context[Context.MessageId];
        if (index >= transactionProcesses.Count)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.BluFinished);
            return;
        }
        
        var transactionProcess = transactionProcesses[index];
        var dateAndTime = MainService.ConvertGregorianToJalaliAndGregorianWithTime(transactionProcess.Date);
        var indexString = $"{index+1}/{transactionProcesses.Count}";
        
        var messageP1 = string.Format(Messages.BluReviewP1, indexString, transactionProcess.Type, dateAndTime);
        var messageP2 = (transactionProcess.Deposit > 0 ? string.Format(Messages.BluReviewP2D, transactionProcess.Deposit) : "") + 
                        (transactionProcess.Withdraw > 0 ? string.Format(Messages.BluReviewP2W, transactionProcess.Withdraw) : "") +
                        string.Format(Messages.BluReviewP2, transactionProcess.BalanceAfter);
        var messageP3 = string.Format(Messages.BluReviewP3, transactionProcess.Description);
        var messageP4 = string.Format(Messages.BluReviewP4, transactionProcess.CategoryName);
        var messageP5 = string.Format(Messages.BluReviewP5, transactionProcess.Title);

        var message = messageP1 + messageP2 + messageP3;
        List<List<Tuple<string, string>>>? collection = [];

        if (session.Action !=  Actions.AwaitingBluReview) return;
        switch (session.CallbackData)
        {
            case CallBacks.WaitForReview:
                message += Messages.BluAsk123;
                collection = [[new(Messages.Add, CallBacks.Add), new(Messages.Ignore, CallBacks.Ignore)]];
                break;
            case CallBacks.AcceptToSave:
                message += Messages.BluAsk4;
                var categories = await tServices.GetCategories(walletId);
                collection = services.LoadCollectionOneClicker(categories, x => x.Id, x => x.Name!);
                break;
            case CallBacks.CategorySelected:
                message += messageP4 + Messages.BluAsk5;
                collection = [[new(Messages.Skip, CallBacks.Skip)]];
                break;
            case CallBacks.TitleSelected:
                message += messageP4 + messageP5 + Messages.BluAsk6;
                collection = [[new (Messages.Done, CallBacks.Done), new (Messages.Cancel, CallBacks.Cancel),]];
                break;
            case CallBacks.Saved:
                message += messageP4 + messageP5 + Messages.BluView;
                collection = null;
                session.SetContext(Context.Index, index + 1);
                session.SetContext(Context.MessageId, 0);
                await ShowBluRow(chatId);
                break;
        }

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Transaction}|{CallBacks.BluAction}|");
        if (loadedMessageId != 0)
        {
            await bot.EditMessageReplyMarkup(chatId: chatId, messageId: loadedMessageId,
                replyMarkup: (InlineKeyboardMarkup)keyboard);
        }
        else
        {
            var messageId = await services.SendMessage(chatId, message, replyMarkup: keyboard);
            session.SetContext(Context.MessageId, messageId);
        }
    }

    private async Task BluAction(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        var transactionProcesses = (List<TransactionProcess>)session.Context[Context.Tps];
        var index = (int)session.Context[Context.Index];
        var walletId = (Guid)session.Context[Context.Wallet];
        var transactionProcess = transactionProcesses[index];

        switch (data.DataSeparated.ElementAtOrDefault(3))
        {
            case CallBacks.Add:
                session.SetCallBack(CallBacks.AcceptToSave);
                break;
            case CallBacks.Ignore:
                session.SetContext(Context.Index, index + 1);
                session.SetCallBack(CallBacks.WaitForReview);
                break;
            case CallBacks.SelectCategory:
                transactionProcess.CategoryId = Guid.Parse(data.DataSeparated.ElementAtOrDefault(4)!);
                session.SetCallBack(CallBacks.CategorySelected);
                break;
            case CallBacks.Skip:
                transactionProcess.Title = transactionProcess.Type + transactionProcess.Description;
                session.SetCallBack(CallBacks.TitleSelected);
                break;
            case CallBacks.Cancel:
                transactionProcess.CategoryId = Guid.Empty;
                transactionProcess.CategoryName = "";
                transactionProcess.Title = "";
                session.SetCallBack(CallBacks.WaitForReview);
                break;
            case CallBacks.Done:
                await tServices.AddTransaction(data.ChatId, walletId, transactionProcess);
                session.SetCallBack(CallBacks.Saved);
                break;
        }
        
        await ShowBluRow(data.ChatId);
    }
    
    public async Task SetTransactionTitle(UpdateData data, string callbackData)
    {
        var transactionTitle = data.MessageText!;
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        if (callbackData != CallBacks.CategorySelected)
        {
            return;
        }
        var transactionProcesses = (List<TransactionProcess>)session.Context[Context.Tps];
        var transactionProcess = transactionProcesses[(int)session.Context[Context.Index]];
        transactionProcess.Title = transactionTitle;
        session.SetCallBack(CallBacks.TitleSelected);
        await ShowBluRow(data.ChatId);
    }
    
    #endregion

    // public async Task AddManualTransaction(UpdateData data, string callbackData)
    // {
    //     var p = callbackData.Split('|');
    //     var parts = (data.MessageText ?? "").Split('|', 2);
    //     if (parts.Length != 2 || !decimal.TryParse(parts[0].Trim().TrimStart('+', '-'), out var amount) ||
    //         (parts[0].Trim()[0] != '+' && parts[0].Trim()[0] != '-'))
    //     {
    //         await services.SendMessage(data.ChatId, Messages.AskManualTransaction);
    //         return;
    //     }
    //
    //     await tServices.AddTransaction(data.ChatId, Guid.Parse(p[0]), Guid.Parse(p[1]), DateTime.Now,
    //         parts[0].Trim()[0] == '+', amount, parts[1]);
    //     sessionService.ClearSession(data.ChatId);
    //     await services.SendMessage(data.ChatId, Messages.TransactionSaved);
    // }
}
