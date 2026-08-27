using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class TransactionHandler(UserSessionService sessionService, MainService services, TransactionService tServices)
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
            
            case CallBacks.GenerateReport when Guid.TryParse(value, out var walletId):
                await StartReport(data, walletId);
                break;
            
            case CallBacks.GenerateReport:
                await HandleReportCallback(data, value!);
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
        var collection = services.LoadCollectionInPages(wallets, callBack, pageNumber, x => x.Id, x => x.Name!, width:2, height:2);
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
            [new(Messages.KeyboardAddTransaction,   $"{CallBacks.AddTransaction}|{walletId}")],
            [new(Messages.KeyboardGenerateReport,   $"{CallBacks.GenerateReport}|{walletId}")],
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
            await services.SendMessage(data.ChatId, string.Format(Messages.WalletJoined, wallet!.Name));
        else
            await services.SendMessage(data.ChatId, Messages.CycleIdIsWrong);
    }
    
    #endregion

    #region Category
    
    private ReplyMarkup? CreateCategoriesKeyboard(List<Category> categories)
    {
        var collection = services.LoadCollectionInScroller(categories, x => x.Id, x => x.Name!, x => x.TempAdded, x => x.TempDeleted);
        return services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Transaction}|{CallBacks.CategoryAction}|");
    }

    private async Task LoadCategories(UpdateData data, string walletIdAsString)
    {
        var isLoaded = Guid.TryParse(walletIdAsString, out var walletId);
        if (!isLoaded) await services.SendMessage(data.ChatId, Messages.WalletLoadFail);
        var categories = await tServices.GetCategoriesByWalletIdAll(walletId);
        var keyboard = CreateCategoriesKeyboard(categories);
        var messageId = await services.SendMessage(data.ChatId, Messages.ScrollerAction, replyMarkup: keyboard);
        sessionService.SetData(chatId: data.ChatId, action: Actions.AwaitingCategoryName, callbackData: $"{messageId}|{walletIdAsString}");
    }
    
    private async Task EditCategoriesKeyboard(long chatId, int messageId, Guid walletId)
    {
        var categories = await tServices.GetCategoriesByWalletIdAll(walletId);
        var keyboard = CreateCategoriesKeyboard(categories);
        await services.EditMessage(chatId, messageId, replyMarkup: keyboard);
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
                await services.DeleteMessage(data.ChatId, messageId);
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
                await services.DeleteMessage(data.ChatId, messageId);
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
        var walletId = Guid.Parse(session.CallbackData);
        var transactionProcesses = new List<TransactionProcess>();
        using var workbook = new XLWorkbook(data.Document!.FileAddress);
        var ws = workbook.Worksheet(1);
        var savedTransactions = await tServices.GetTransactionByWalletAndUser(data.ChatId, walletId);
        for (var row = 12; !ws.Cell(row, 19).IsEmpty(); row++)
        {
            var input = ws.Cell(row, 16).GetString();
            var digitsOnly = Regex.Replace(input, @"[^\d]", "");
            var documentNo = long.Parse(digitsOnly);

            if (savedTransactions.Any(x => x.DocumentNo == documentNo)) break;
            transactionProcesses.Add(new TransactionProcess
            {
                Index = ws.Cell(row, 19).GetValue<int>(),
                Date = MainService.ConvertJalaliToGregorian(ws.Cell(row, 18).GetString()),
                Type = ws.Cell(row, 7).GetString(),
                Description = ws.Cell(row, 11).GetString(),
                DocumentNo = documentNo,
                Deposit = ws.Cell(row, 4).GetValue<decimal>()/10,
                Withdraw = ws.Cell(row, 3).GetValue<decimal>()/10,
                BalanceAfter = ws.Cell(row, 2).GetValue<decimal>()/10,
                Processed = false
            });
        }
        File.Delete(data.Document!.FileAddress);
        transactionProcesses.Reverse();
        session.SetAction(Actions.AwaitingBluReview);
        session.SetContext(Context.Tps, transactionProcesses);
        session.SetContext(Context.Wallet, walletId);
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
                collection = [[new(Messages.Add, CallBacks.Add), new(Messages.Ignore, CallBacks.Ignore)],
                    [new(Messages.Split, CallBacks.Split)],
                ];
                break;
            case CallBacks.SelectSplitCount:
                message += Messages.BluAsk1234;
                var numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                collection = services.LoadCollectionOneClicker(numbers, prefixCallbackData: CallBacks.SelectSplitCount, width:3);
                break;
            case CallBacks.AcceptToSave:
                message += Messages.BluAsk4;
                var categories = await tServices.GetCategories(walletId);
                collection = services.LoadCollectionOneClicker(categories.OrderBy(x => x.CreateTime).ToList(), x => x.Id, x => x.Name!, prefixCallbackData: CallBacks.SelectCategory, width:3);
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
                session.SetCallBack(CallBacks.WaitForReview);
                await ShowBluRow(chatId);
                break;
        }

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Transaction}|{CallBacks.BluAction}|");
        if (loadedMessageId != 0)
        {
            await services.EditMessage(chatId, loadedMessageId, message, keyboard);
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

        switch (data.DataSeparated.ElementAtOrDefault(2))
        {
            case CallBacks.Add:
                session.SetCallBack(CallBacks.AcceptToSave);
                break;
            case CallBacks.Ignore:
                session.SetContext(Context.Index, index + 1);
                session.SetCallBack(CallBacks.WaitForReview);
                break;
            case CallBacks.Split:
                session.SetCallBack(CallBacks.SelectSplitCount);
                break;
            case CallBacks.SelectSplitCount:
                var splitCountString = data.DataSeparated.ElementAtOrDefault(3);
                if (!int.TryParse(splitCountString, out var splitCount)) return;
                if (splitCount is < 1 or > 9) return;
                SplitTransaction(transactionProcesses, index, splitCount);
                session.SetCallBack(CallBacks.AcceptToSave);
                break;
            case CallBacks.SelectCategory:
                var category = await tServices.GetCategoryByCategoryId(Guid.Parse(data.DataSeparated.ElementAtOrDefault(3)!));
                transactionProcess.CategoryId = category!.Id;
                transactionProcess.CategoryName = category.Name!;
                session.SetCallBack(CallBacks.CategorySelected);
                break;
            case CallBacks.Skip:
                transactionProcess.Title = transactionProcess.Type + " - " + transactionProcess.Description;
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

    private void SplitTransaction(List<TransactionProcess> transactions, int index, int splitCount)
    {
        var original = transactions[index];
        var totalAmount = original.Deposit > 0 ? original.Deposit : original.Withdraw;
        var amountPerTransaction = totalAmount / splitCount;
        var originalBalanceAfter = original.BalanceAfter;
        var splitTransactions = new List<TransactionProcess>();
        var balanceBefore = original.Deposit > 0
            ? originalBalanceAfter - original.Deposit
            : originalBalanceAfter + original.Withdraw;

        for (var i = 1; i <= splitCount; i++)
        {
            var transaction = new TransactionProcess
            {
                Index = original.Index,
                Date = original.Date,
                Type = original.Type,
                Description = original.Description,
                DocumentNo = original.DocumentNo,
                Deposit = 0,
                Withdraw = 0,
                BalanceAfter = 0,
                Processed = false,
                CategoryId = original.CategoryId,
                CategoryName = original.CategoryName,
                Title = original.Title
            };

            if (original.Deposit > 0)
            {
                transaction.Deposit = amountPerTransaction;
                transaction.BalanceAfter =
                    balanceBefore + (amountPerTransaction * i);
            }
            else
            {
                transaction.Withdraw = amountPerTransaction;
                transaction.BalanceAfter =
                    balanceBefore - (amountPerTransaction * i);
            }

            transaction.Description = original.Description + " (" + i + ")";
            transaction.Title = original.Title + " (" + i + ")";
            splitTransactions.Add(transaction);
        }

        transactions.RemoveAt(index);
        transactions.InsertRange(index, splitTransactions);
    }

    public async Task SetTransactionTitle(UpdateData data)
    {
        var transactionTitle = data.MessageText!;
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        var transactionProcesses = (List<TransactionProcess>)session.Context[Context.Tps];
        var transactionProcess = transactionProcesses[(int)session.Context[Context.Index]];
        transactionProcess.Title = transactionTitle;
        session.SetCallBack(CallBacks.TitleSelected);
        await ShowBluRow(data.ChatId);
    }
    
    #endregion
    
    #region Report

    private async Task StartReport(UpdateData data, Guid walletId)
    {
        sessionService.SetData(data.ChatId, Actions.BuildingReport, "");
        var selectedIds = new List<Guid>();
        var session = sessionService.GetData(data.ChatId)!;
        session.SetContext(Context.ReportWalletId, walletId);
        session.SetContext(Context.ReportSelectedCategories, selectedIds);
        session.SetContext(Context.ReportAllSelected, false);
        session.SetContext(Context.StartDate, DateTime.MinValue);
        session.SetContext(Context.EndDate, DateTime.MaxValue);
        session.SetContext(Context.ReportAllSelected, false);
        session.SetContext(Context.ReportMessageId, 0);

        await AskDateRange(data);
    }

    private async Task AskDateRange(UpdateData data)
    {
        var (previous, current, next) = MainService.LoadJalaliFirstOfMonths(DateTime.Now);
        var collection = new List<List<Tuple<string, string>>>
        {
            new() { new (Messages.ThisMonth,    $"{MainService.GregorianToSimplified(current)}|{MainService.GregorianToSimplified(next)}"), },
            new() { new (Messages.LastMonth,    $"{MainService.GregorianToSimplified(previous)}|{MainService.GregorianToSimplified(current)}"), },
            new() { new (Messages.AllTime,      $""), },
            new() { new (Messages.CustomPeriod, $"{CallBacks.CustomPeriod}"), }
        };
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}|{CallBacks.GenerateReport}|{CallBacks.SelectDate}|");
        await services.SendMessage(data.ChatId, Messages.SelectDate, replyMarkup: keyboard);
    }

    private async Task ShowCategorySelection(long chatId)
    {
        var session = sessionService.GetData(chatId);
        if (session == null) return;
        
        var walletId = (Guid)session.Context[Context.ReportWalletId];
        var selectedIds = (List<Guid>)session.Context[Context.ReportSelectedCategories];
        var allSelected = (bool)session.Context[Context.ReportAllSelected];
        var messageId = (int)session.Context[Context.ReportMessageId];

        var categories = await tServices.GetCategoriesByWalletId(walletId);
        
        if (categories.Count == 0)
        {
            await services.SendMessage(chatId, Messages.NoCategoryInWallet);
            return;
        }
        
        var collection = services.LoadCollectionMultiSelect(categories, selectedIds, allSelected, category => category.Id, category => category.Name!);
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Transaction}|{CallBacks.GenerateReport}|");
        var message = string.Format(Messages.ReportCategorySelection, selectedIds.Count , categories.Count(c => !c.TempDeleted));

        if (messageId == 0)
        {
            var newMessageId = await services.SendMessage(chatId, message, replyMarkup: keyboard);
            session.SetContext(Context.ReportMessageId, newMessageId);
            return;
        }
        await services.EditMessage(chatId, messageId, message, keyboard);
    }

    private async Task HandleReportCallback(UpdateData data, string action)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        
        var walletId = (Guid)session.Context[Context.ReportWalletId];
        var selectedIds = (List<Guid>)session.Context[Context.ReportSelectedCategories];
        var messageId = (int)session.Context[Context.ReportMessageId];
        
        switch (action)
        {
            case CallBacks.SelectDate:
                var start = data.DataSeparated.ElementAtOrDefault(3);
                var end = data.DataSeparated.ElementAtOrDefault(4);
                
                if (start == CallBacks.CustomPeriod)
                {
                    await AskCustomPeriod(data.ChatId, true);
                    return;
                }

                if (!string.IsNullOrEmpty(start)) session.SetContext(Context.StartDate, MainService.SimplifiedToGregorian(start));
                if (!string.IsNullOrEmpty(end)) session.SetContext(Context.EndDate, MainService.SimplifiedToGregorian(end));
                await ShowCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleSelectToggle:
                var categoryId = Guid.Parse(data.DataSeparated.ElementAtOrDefault(3)!);
                if (!selectedIds.Remove(categoryId)) selectedIds.Add(categoryId);
                session.SetContext(Context.ReportSelectedCategories, selectedIds);
                await ShowCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleSelectAll:
                var allCategories = await tServices.GetCategoriesByWalletId(walletId);
                selectedIds = new List<Guid>(allCategories.Select(c => c.Id));
                session.SetContext(Context.ReportSelectedCategories, selectedIds);
                session.SetContext(Context.ReportAllSelected, true);
                await ShowCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleDeselectAll:
                selectedIds.Clear();
                session.SetContext(Context.ReportSelectedCategories, selectedIds);
                session.SetContext(Context.ReportAllSelected, false);
                await ShowCategorySelection(data.ChatId);
                break;
            case CallBacks.Done:
                await services.DeleteMessage(data.ChatId, messageId);
                await GenerateWalletReport(data.ChatId);
                break;
            case CallBacks.Cancel:
                await services.DeleteMessage(data.ChatId, messageId);
                sessionService.ClearSession(data.ChatId);
                await services.SendMessage(data.ChatId, Messages.ReportCancelled);
                break;
        }
    }

    public async Task AskCustomPeriod(long chatId, bool isStart)
    {
        if (isStart)
            await services.SendDatePicker(chatId, DatePickerMethods.CustomStartTransactionReport, Messages.CustomStart);
        else 
            await services.SendDatePicker(chatId, DatePickerMethods.CustomEndTransactionReport, Messages.CustomEnd);
    }

    public async Task SetCustomPeriod(long chatId, DateTime date, bool isStart)
    {
        var session = sessionService.GetData(chatId);
        if (session == null) return;

        if (isStart)
        {
            session.SetContext(Context.StartDate, date);
            await AskCustomPeriod(chatId, false);
        }
        else
        {
            session.SetContext(Context.EndDate, date);
            await ShowCategorySelection(chatId);
        }
    }
    
    private async Task GenerateWalletReport(long chatId)
    {
        var session = sessionService.GetData(chatId);
        if (session == null) return;
        
        var walletId = (Guid)session.Context[Context.ReportWalletId];
        var selectedCategoryIds = (List<Guid>)session.Context[Context.ReportSelectedCategories];
        var startDate = (DateTime?)session.Context[Context.StartDate];
        var endDate = (DateTime?)session.Context[Context.EndDate];

        await services.SendMessage(chatId, Messages.ReportGenerating);

        var report = await tServices.GetReportData(walletId, selectedCategoryIds, startDate, endDate);
        
        var pdfBytes = tServices.GeneratePdf(report);
        using var stream = new MemoryStream(pdfBytes);
        await services.SendMessage(chatId, string.Format(Messages.ReportReady, report.WalletName, $"{report.GeneratedAt:yyyy/MM/dd HH:mm}", report.Transactions.Count()),
            document: new InputFileStream(stream, string.Format(Files.PdfWalletReport, $"{DateTime.Now:yyyyMMdd_HHmmss}")));
        
        var excelBytes = tServices.GenerateExcel(report);
        using var excelStream = new MemoryStream(excelBytes);
        await services.SendMessage(chatId, Messages.ExcelCaption,
            document: new InputFileStream(excelStream, string.Format(Files.ExcelWalletReport, $"{DateTime.Now:yyyyMMdd_HHmmss}")));
        
        sessionService.ClearSession(chatId);
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