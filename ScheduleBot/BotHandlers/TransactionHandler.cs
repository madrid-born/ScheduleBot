using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Word;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Document = QuestPDF.Fluent.Document;

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
            
            case CallBacks.GenerateReport when Guid.TryParse(value, out var walletId):
                await StartReport(data, walletId);
                break;
            
            case CallBacks.GenerateReport:
                await HandleReportCallback(data, action);
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
        var walletId = Guid.Parse(session.CallbackData);
        var transactionProcesses = new List<TransactionProcess>();
        using var workbook = new XLWorkbook(data.Document!.FileAddress);
        var ws = workbook.Worksheet(1);
        var savedTransactions = await tServices.GetTransactionByWalletAndUser(data.ChatId, walletId);
        for (var row = 12; !ws.Cell(row, 19).IsEmpty(); row++)
        {
            if (savedTransactions.Any(x => x.DocumentNo == long.Parse(ws.Cell(row, 16).GetString()))) break;
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
                collection = [[new(Messages.Add, CallBacks.Add), new(Messages.Ignore, CallBacks.Ignore)]];
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
            await bot.EditMessageText(chatId: chatId, messageId: loadedMessageId, text: message,
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

        //todo : split the amount
        switch (data.DataSeparated.ElementAtOrDefault(2))
        {
            case CallBacks.Add:
                session.SetCallBack(CallBacks.AcceptToSave);
                break;
            case CallBacks.Ignore:
                session.SetContext(Context.Index, index + 1);
                session.SetCallBack(CallBacks.WaitForReview);
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
    
    #region Report

    private async Task StartReport(UpdateData data, Guid walletId)
    {
        sessionService.SetData(data.ChatId, Actions.BuildingReport, "");
        var selectedIds = new List<Guid>();
        var session = sessionService.GetData(data.ChatId)!;
        session.SetContext(Context.ReportWalletId, walletId);
        session.SetContext(Context.ReportSelectedCategories, selectedIds);
        session.SetContext(Context.ReportAllSelected, false);
        
        await ShowCategorySelection(data.ChatId, walletId, selectedIds, false, 0);
    }

    private async Task ShowCategorySelection(long chatId, Guid walletId, List<Guid> selectedIds, bool allSelected, int messageId = 0)
    {
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
            var session = sessionService.GetData(chatId)!;
            session.SetContext(Context.ReportMessageId, newMessageId);
            return;
        }
        //todo : add edit message to main service
        await bot.EditMessageText(chatId: chatId, messageId: messageId, text: message,
            replyMarkup: (InlineKeyboardMarkup)keyboard);
    }

    private async Task HandleReportCallback(UpdateData data, string action)
    {
        var value = data.DataSeparated.ElementAtOrDefault(3);
        
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        
        var walletId = (Guid)session.Context[Context.ReportWalletId];
        var selectedIds = (List<Guid>)session.Context[Context.ReportSelectedCategories];
        var allSelected = (bool)session.Context[Context.ReportAllSelected];
        var messageId = (int)session.Context[Context.ReportMessageId];
        
        switch (action)
        {
            case CallBacks.MultipleSelectToggle:
                var categoryId = Guid.Parse(value);
                if (selectedIds.Contains(categoryId))
                    selectedIds.Remove(categoryId);
                else
                    selectedIds.Add(categoryId);
                
                // Update session
                session.Context[Context.ReportSelectedCategories] = selectedIds;
                await ShowCategorySelection(data.ChatId, walletId, selectedIds, allSelected, messageId);
                break;
                
            case CallBacks.MultipleSelectAll:
                var allCategories = await tServices.GetCategoriesByWalletId(walletId);
                selectedIds = new List<Guid>(allCategories.Where(c => !c.TempDeleted).Select(c => c.Id));
                session.Context[Context.ReportSelectedCategories] = selectedIds;
                session.Context[Context.ReportAllSelected] = true;
                await ShowCategorySelection(data.ChatId, walletId, selectedIds, true, messageId);
                break;
                
            case CallBacks.MultipleDeselectAll:
                selectedIds.Clear();
                session.Context[Context.ReportSelectedCategories] = selectedIds;
                session.Context[Context.ReportAllSelected] = false;
                await ShowCategorySelection(data.ChatId, walletId, selectedIds, false, messageId);
                break;
                
            case CallBacks.ReportContinue:
                // Generate the report
                await GenerateWalletReport(data.ChatId, walletId, selectedIds);
                break;
                
            case CallBacks.Cancel:
                sessionService.ClearSession(data.ChatId);
                await services.SendMessage(data.ChatId, "❌ Report generation cancelled.");
                break;
        }
    }
    
    private async Task GenerateWalletReport(long chatId, Guid walletId, List<Guid> selectedCategoryIds)
    {
        await services.SendMessage(chatId, Messages.ReportGenerating);
    
        try
        {
            // Get report data
            var reportData = await tServices.GetReportData(walletId, selectedCategoryIds);
        
            if (!reportData.Transactions.Any())
            {
                await services.SendMessage(chatId, Messages.ReportNoTransactions);
                sessionService.ClearSession(chatId);
                return;
            }
        
            // Build the report
            var reportBuilder = new WalletReportBuilder();
            var report = reportBuilder.BuildReport(reportData);
        
            // Generate PDF
            var pdfGenerator = new WalletReportPdfGenerator();
            var pdfBytes = pdfGenerator.GeneratePdf(report);
        
            // Send PDF
            var fileName = $"Wallet_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            using var stream = new MemoryStream(pdfBytes);
            await bot.SendDocument(
                chatId: chatId,
                document: new InputFileStream(stream, fileName),
                caption: $"📊 {Messages.ReportReady}\n\nWallet: {report.WalletName}\nGenerated: {report.GeneratedAt:yyyy/MM/dd HH:mm}\nTransactions: {report.Transactions.Count()}"
            );
            
            // Generate Excel
            var excelBytes = pdfGenerator.GenerateExcel(report);

            // Send Excel
            var excelFileName = $"Wallet_Transactions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            using var excelStream = new MemoryStream(excelBytes);
            await bot.SendDocument(
                chatId: chatId,
                document: new InputFileStream(excelStream, excelFileName),
                caption: $"📊 Transaction details in Excel format"
            );

            // Clear session
            sessionService.ClearSession(chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating report");
            await services.SendMessage(chatId, $"❌ Error generating report: {ex.Message}");
        }
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

//
// using ScheduleBot.Models;
// using ScheduleBot.Models.Report;
//
// namespace ScheduleBot.Services;

public class WalletReportBuilder
{
    public WalletReport BuildReport(ReportData data)
    {
        var report = new WalletReport
        {
            WalletName = data.Wallet.Name ?? "Unnamed Wallet",
            GeneratedAt = DateTime.Now,
            Transactions = data.Transactions,
            TotalTransactions = data.Transactions.Count,
            FromDate = data.Transactions.Any() ? data.Transactions.Min(t => t.Date) : (DateTime?)null,
            ToDate = data.Transactions.Any() ? data.Transactions.Max(t => t.Date) : (DateTime?)null,
        };
        
        // Build summary
        report.Summary = BuildSummary(data.Transactions);
        
        // Build category reports
        report.CategoryReports = BuildCategoryReports(data);
        
        // Build monthly reports
        report.MonthlyReports = BuildMonthlyReports(data.Transactions);
        
        // Build user reports
        report.UserReports = BuildUserReports(data);
        
        return report;
    }
    
    private ReportSummary BuildSummary(List<TransactionRecord> transactions)
    {
        if (!transactions.Any())
            return new ReportSummary();
        
        var deposits = transactions.Where(t => t.Deposit > 0).ToList();
        var withdrawals = transactions.Where(t => t.Withdraw > 0).ToList();
        
        var summary = new ReportSummary
        {
            TotalDeposits = deposits.Sum(t => t.Deposit),
            TotalWithdrawals = withdrawals.Sum(t => t.Withdraw),
            DepositCount = deposits.Count,
            WithdrawCount = withdrawals.Count,
            NetCashFlow = deposits.Sum(t => t.Deposit) - withdrawals.Sum(t => t.Withdraw),
            LargestDeposit = deposits.Any() ? deposits.Max(t => t.Deposit) : 0,
            LargestWithdrawal = withdrawals.Any() ? withdrawals.Max(t => t.Withdraw) : 0,
            EarliestTransaction = transactions.Min(t => t.Date),
            LatestTransaction = transactions.Max(t => t.Date),
            AverageTransaction = transactions.Any() ? transactions.Average(t => t.Deposit + t.Withdraw) : 0,
            AverageDeposit = deposits.Any() ? deposits.Average(t => t.Deposit) : 0,
            AverageWithdrawal = withdrawals.Any() ? withdrawals.Average(t => t.Withdraw) : 0
        };
        
        // Calculate opening and closing balance
        if (transactions.Any())
        {
            summary.OpeningBalance = transactions.First().BalanceAfter - (transactions.First().Deposit - transactions.First().Withdraw);
            summary.ClosingBalance = transactions.Last().BalanceAfter;
        }
        
        return summary;
    }
    
    private List<CategoryReport> BuildCategoryReports(ReportData data)
    {
        var reports = new List<CategoryReport>();
        var totalWithdrawals = data.Transactions.Where(t => t.Withdraw > 0).Sum(t => t.Withdraw);
        var totalDeposits = data.Transactions.Where(t => t.Deposit > 0).Sum(t => t.Deposit);
        
        foreach (var category in data.Categories)
        {
            if (!data.TransactionsByCategory.ContainsKey(category.Id) || !data.TransactionsByCategory[category.Id].Any())
                continue;
            
            var categoryTransactions = data.TransactionsByCategory[category.Id];
            var deposits = categoryTransactions.Where(t => t.Deposit > 0).ToList();
            var withdrawals = categoryTransactions.Where(t => t.Withdraw > 0).ToList();
            
            var report = new CategoryReport
            {
                CategoryId = category.Id,
                CategoryName = category.Name ?? "Unnamed Category",
                TransactionCount = categoryTransactions.Count,
                TotalDeposits = deposits.Sum(t => t.Deposit),
                TotalWithdrawals = withdrawals.Sum(t => t.Withdraw),
                NetCashFlow = deposits.Sum(t => t.Deposit) - withdrawals.Sum(t => t.Withdraw),
                PercentageOfTotalWithdrawals = totalWithdrawals > 0 ? (withdrawals.Sum(t => t.Withdraw) / totalWithdrawals) * 100 : 0,
                PercentageOfTotalDeposits = totalDeposits > 0 ? (deposits.Sum(t => t.Deposit) / totalDeposits) * 100 : 0,
                AverageTransaction = categoryTransactions.Average(t => t.Deposit + t.Withdraw),
                LargestTransaction = categoryTransactions.Max(t => t.Deposit + t.Withdraw),
                FirstTransaction = categoryTransactions.Min(t => t.Date),
                LastTransaction = categoryTransactions.Max(t => t.Date)
            };
            
            reports.Add(report);
        }
        
        return reports.OrderByDescending(r => r.TotalWithdrawals).ToList();
    }
    
    private List<MonthlyReport> BuildMonthlyReports(List<TransactionRecord> transactions)
    {
        var reports = new List<MonthlyReport>();
        
        var monthlyGroups = transactions
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month);
        
        decimal runningBalance = 0;
        
        foreach (var group in monthlyGroups)
        {
            var monthlyTransactions = group.ToList();
            var deposits = monthlyTransactions.Where(t => t.Deposit > 0).Sum(t => t.Deposit);
            var withdrawals = monthlyTransactions.Where(t => t.Withdraw > 0).Sum(t => t.Withdraw);
            var netCashFlow = deposits - withdrawals;
            runningBalance += netCashFlow;
            
            var report = new MonthlyReport
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                MonthName = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMMM"),
                TransactionCount = monthlyTransactions.Count,
                Deposits = deposits,
                Withdrawals = withdrawals,
                NetCashFlow = netCashFlow,
                ClosingBalance = monthlyTransactions.Last().BalanceAfter
            };
            
            reports.Add(report);
        }
        
        return reports;
    }
    
    private List<UserReport> BuildUserReports(ReportData data)
    {
        var reports = new List<UserReport>();
        
        var userGroups = data.Transactions
            .GroupBy(t => t.ConsumerId)
            .Select(g => new
            {
                UserId = g.Key,
                Transactions = g.ToList()
            });
        
        foreach (var group in userGroups)
        {
            var user = data.UserMap.ContainsKey(group.UserId) ? data.UserMap[group.UserId] : null;
            var userName = user?.Name ?? "Unknown User";
            
            var deposits = group.Transactions.Where(t => t.Deposit > 0).Sum(t => t.Deposit);
            var withdrawals = group.Transactions.Where(t => t.Withdraw > 0).Sum(t => t.Withdraw);
            
            var report = new UserReport
            {
                UserId = group.UserId,
                UserName = userName,
                TransactionCount = group.Transactions.Count,
                Deposits = deposits,
                Withdrawals = withdrawals,
                NetCashFlow = deposits - withdrawals
            };
            
            reports.Add(report);
        }
        
        return reports.OrderByDescending(r => r.NetCashFlow).ToList();
    }
}

// using QuestPDF.Fluent;
// using QuestPDF.Helpers;
// using QuestPDF.Infrastructure;
// using ScheduleBot.Models.Report;
//
// namespace ScheduleBot.Services;

public class WalletReportPdfGenerator
{
    public byte[] GeneratePdf(WalletReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));
                
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
                
                void ComposeHeader(IContainer container)
                {
                    container.Column(col =>
                    {
                        col.Spacing(5);
                        
                        col.Item().Text("📊 WALLET FINANCIAL REPORT")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);
                        
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(infoCol =>
                            {
                                infoCol.Item().Text($"Wallet: {report.WalletName}").FontSize(12);
                                infoCol.Item().Text($"Generated: {report.GeneratedAt:yyyy/MM/dd HH:mm:ss}").FontSize(10).FontColor(Colors.Grey.Medium);
                                if (report.FromDate.HasValue && report.ToDate.HasValue)
                                {
                                    infoCol.Item().Text($"Period: {report.FromDate:yyyy/MM/dd} - {report.ToDate:yyyy/MM/dd}").FontSize(10);
                                }
                                infoCol.Item().Text($"Total Transactions: {report.TotalTransactions}").FontSize(11).Bold();
                            });
                            
                            row.ConstantItem(150).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(summaryBox =>
                            {
                                summaryBox.Item().Text("Quick Stats").FontSize(10).Bold().AlignCenter();
                                summaryBox.Item().Text($"Deposits: {report.Summary.TotalDeposits:N0}").FontSize(9);
                                summaryBox.Item().Text($"Withdrawals: {report.Summary.TotalWithdrawals:N0}").FontSize(9);
                                summaryBox.Item().Text($"Net: {report.Summary.NetCashFlow:N0}").FontSize(9)
                                    .FontColor(report.Summary.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        });
                    });
                }
                
                void ComposeContent(IContainer container)
                {
                    container.Column(col =>
                    {
                        col.Spacing(15);
                        
                        // 1. Financial Summary
                        col.Item().Element(ComposeFinancialSummary);
                        
                        // 2. Category Analysis
                        if (report.CategoryReports.Any())
                            col.Item().Element(ComposeCategoryAnalysis);
                        
                        // 3. Monthly Summary
                        if (report.MonthlyReports.Any())
                            col.Item().Element(ComposeMonthlySummary);
                        
                        // 4. User Activity
                        if (report.UserReports.Any())
                            col.Item().Element(ComposeUserActivity);
                        
                        // 5. Transaction Details
                        if (report.Transactions.Any())
                            col.Item().Element(ComposeTransactionDetails);
                    });
                }
                
                void ComposeFinancialSummary(IContainer container)
                {
                    container.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📈 FINANCIAL SUMMARY").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Metric").Bold().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Amount").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Average").Bold().AlignRight().FontSize(10);
                            });
                            
                            void AddRow(string metric, decimal amount, int count, decimal avg)
                            {
                                table.Cell().Padding(3).Text(metric);
                                table.Cell().Padding(3).Text($"{amount:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{count}").AlignRight();
                                table.Cell().Padding(3).Text($"{avg:N0}").AlignRight();
                            }
                            
                            AddRow("Deposits", report.Summary.TotalDeposits, report.Summary.DepositCount, report.Summary.AverageDeposit);
                            AddRow("Withdrawals", report.Summary.TotalWithdrawals, report.Summary.WithdrawCount, report.Summary.AverageWithdrawal);
                            
                            table.Cell().Padding(3).Text("Net Cash Flow").Bold();
                            table.Cell().Padding(3).Text($"{report.Summary.NetCashFlow:N0}").AlignRight().FontColor(report.Summary.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();
                            table.Cell().Padding(3).Text("").AlignRight();
                            table.Cell().Padding(3).Text("").AlignRight();
                            
                            // AddRow("Opening Balance", report.Summary.OpeningBalance, 0, 0);
                            // AddRow("Closing Balance", report.Summary.ClosingBalance, 0, 0);
                            // AddRow("Largest Transaction", report.Summary.LargestDeposit > report.Summary.LargestWithdrawal ? report.Summary.LargestDeposit : report.Summary.LargestWithdrawal, 0, 0);
                            // AddRow("Average Transaction", report.Summary.AverageTransaction, 0, 0);
                        });
                    });
                }
                
                void ComposeCategoryAnalysis(IContainer container)
                {
                    container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📊 CATEGORY ANALYSIS").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Category").Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposits").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdrawals").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Net").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("% of Total").Bold().AlignRight().FontSize(9);
                            });
                            
                            // Show ALL categories
                            var rowIndex = 0;
                            foreach (var categoryReport in report.CategoryReports)
                            {
                                var backgroundColor = rowIndex++ % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                table.Cell().Padding(3).Background(backgroundColor).Text(categoryReport.CategoryName);
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.TransactionCount}").AlignRight();
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.TotalDeposits:N0}").AlignRight();
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.TotalWithdrawals:N0}").AlignRight();
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.NetCashFlow:N0}").AlignRight().FontColor(categoryReport.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.PercentageOfTotalWithdrawals:F1}%").AlignRight();
                            }
                            
                            // Add Total row
                            table.Cell().Padding(3).Text("TOTAL").Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.TransactionCount)}").AlignRight().Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.TotalDeposits):N0}").AlignRight().Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.TotalWithdrawals):N0}").AlignRight().Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.NetCashFlow):N0}").AlignRight().Bold();
                            table.Cell().Padding(3).Text("100%").AlignRight().Bold();
                        });
                    });
                }
                
                void ComposeMonthlySummary(IContainer container)
                {
                    container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📅 MONTHLY SUMMARY").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Month").Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposits").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdrawals").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Net").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Closing Balance").Bold().AlignRight().FontSize(9);
                            });
                            
                            // Show ALL months
                            foreach (var month in report.MonthlyReports)
                            {
                                table.Cell().Padding(3).Text($"{month.MonthName} {month.Year}");
                                table.Cell().Padding(3).Text($"{month.TransactionCount}").AlignRight();
                                table.Cell().Padding(3).Text($"{month.Deposits:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{month.Withdrawals:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{month.NetCashFlow:N0}").AlignRight()
                                    .FontColor(month.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Padding(3).Text($"{month.ClosingBalance:N0}").AlignRight();
                            }
                        });
                    });
                }
                
                void ComposeUserActivity(IContainer container)
                {
                    container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("👥 USER ACTIVITY").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("User").Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposits").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdrawals").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Net").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Avg/Transaction").Bold().AlignRight().FontSize(9);
                            });
                            
                            // Show ALL users
                            foreach (var user in report.UserReports)
                            {
                                var avgPerTransaction = user.TransactionCount > 0 
                                    ? (user.Deposits + user.Withdrawals) / user.TransactionCount 
                                    : 0;
                                
                                table.Cell().Padding(3).Text(user.UserName);
                                table.Cell().Padding(3).Text($"{user.TransactionCount}").AlignRight();
                                table.Cell().Padding(3).Text($"{user.Deposits:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{user.Withdrawals:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{user.NetCashFlow:N0}").AlignRight()
                                    .FontColor(user.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Padding(3).Text($"{avgPerTransaction:N0}").AlignRight();
                            }
                        });
                    });
                }
                
                void ComposeTransactionDetails(IContainer container)
                {
                    container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📋 TRANSACTION DETAILS").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(0.5f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Date").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Time").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Consumer").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Category").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Title").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdraw").Bold().AlignRight().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposit").Bold().AlignRight().FontSize(8);
                            });
                            
                            // Show ALL transactions
                            var rowIndex = 0;

                            foreach (var transaction in report.Transactions)
                            {
                                var categoryName = report.CategoryReports.FirstOrDefault(c => c.CategoryId == transaction.CategoryId)?.CategoryName ?? "Unknown";
                                var consumerName = report.UserReports.FirstOrDefault(u => u.UserId == transaction.ConsumerId)?.UserName ?? "Unknown User";
                                var backgroundColor = rowIndex++ % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Padding(3).Background(backgroundColor).Text($"{MainService.ConvertGregorianToJalali(transaction.Date)}").FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{transaction.Date:HH:mm}").FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(consumerName).FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(categoryName).FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(transaction.Title).FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(transaction.Withdraw > 0 ? $"{transaction.Withdraw:N0}" : "").AlignRight().FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(transaction.Deposit > 0 ? $"{transaction.Deposit:N0}" : "").AlignRight().FontSize(8);
                            }
                        });
                    });
                }
                
                void ComposeFooter(IContainer container)
                {
                    container.AlignCenter().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        col.Spacing(3);
                        col.Item().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        })/*.FontSize(8).FontColor(Colors.Grey.Medium)*/;
                    });
                }
            });
        });
        
        return document.GeneratePdf();
    }
    
    public byte[] GenerateExcel(WalletReport report)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");
        
        // Set headers
        var headers = new[] { "Date", "Time", "Consumer", "Category", "Title", "Withdraw", "Deposit" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }
        
        // Add data rows
        int row = 2;
        foreach (var transaction in report.Transactions)
        {
            var categoryName = report.CategoryReports.FirstOrDefault(c => c.CategoryId == transaction.CategoryId)?.CategoryName ?? "Unknown";
            var consumerName = report.UserReports.FirstOrDefault(u => u.UserId == transaction.ConsumerId)?.UserName ?? "Unknown User";
            
            worksheet.Cell(row, 1).Value = MainService.ConvertGregorianToJalali(transaction.Date);
            worksheet.Cell(row, 2).Value = transaction.Date.ToString("HH:mm");
            worksheet.Cell(row, 3).Value = consumerName;
            worksheet.Cell(row, 4).Value = categoryName;
            worksheet.Cell(row, 5).Value = transaction.Title ?? "";
            worksheet.Cell(row, 6).Value = transaction.Withdraw > 0 ? transaction.Withdraw : 0;
            worksheet.Cell(row, 7).Value = transaction.Deposit > 0 ? transaction.Deposit : 0;
            row++;
        }
        
        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
        
        // Add a summary sheet
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Financial Summary";
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(1, 1).Style.Font.FontSize = 14;
        
        var summaryData = new[]
        {
            new { Metric = "Total Deposits", Value = report.Summary.TotalDeposits },
            new { Metric = "Total Withdrawals", Value = report.Summary.TotalWithdrawals },
            new { Metric = "Net Cash Flow", Value = report.Summary.NetCashFlow },
            new { Metric = "Opening Balance", Value = report.Summary.OpeningBalance },
            new { Metric = "Closing Balance", Value = report.Summary.ClosingBalance },
            new { Metric = "Transaction Count", Value = (decimal)report.TotalTransactions }
        };
        
        int summaryRow = 3;
        foreach (var item in summaryData)
        {
            summarySheet.Cell(summaryRow, 1).Value = item.Metric;
            summarySheet.Cell(summaryRow, 2).Value = item.Value;
            summaryRow++;
        }
        
        summarySheet.Columns().AdjustToContents();
        
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}