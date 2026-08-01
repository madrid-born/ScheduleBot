using ClosedXML.Excel;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public class TransactionHandler(ITelegramBotClient bot, IServiceProvider serviceProvider, IConfiguration configuration,
    UserSessionService sessionService, MainService services, TransactionService tServices, ILogger<CycleTrackerHandler> logger)
{
    public async Task HandleSection(UpdateData data)
    {
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.KeyboardWalletManagement,   CallBacks.WalletManagement)],
            [new(Messages.KeyboardCreateWallet,       CallBacks.CreateWallet)]
        ];
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}\\{CallBacks.MainSection}\\");
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
            case CallBacks.ManageCategories when Guid.TryParse(value, out var walletId):
                await CategoryMenu(data, walletId);
                break;
            case CallBacks.AddCategory when Guid.TryParse(value, out var walletId):
                sessionService.SetData(data.ChatId, Actions.AwaitingCategoryName, walletId.ToString());
                await services.SendMessage(data.ChatId, Messages.AskCategoryName, replyMarkup: new ForceReplyMarkup());
                break;
            case CallBacks.AddTransaction when Guid.TryParse(value, out var walletId):
                await AddTransactionMenu(data.ChatId, walletId);
                break;
            case CallBacks.ManualTransaction when Guid.TryParse(value, out var walletId):
                await SelectCategory(data.ChatId, walletId, CallBacks.ManualTransaction);
                break;
            case CallBacks.BluTransaction when Guid.TryParse(value, out var walletId):
                sessionService.SetData(data.ChatId, Actions.AwaitingBluFile, walletId.ToString());
                await services.SendMessage(data.ChatId, Messages.BluFilePrompt);
                break;
            case CallBacks.BluAction:
                await BluAction(data);
                break;
            case CallBacks.JoinWallet when Guid.TryParse(value, out var walletId):
                await tServices.InviteAccept(data.ChatId, walletId); await services.SendMessage(data.ChatId, Messages.WalletJoined);
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
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}\\");
        await services.SendMessage(chatId, Messages.SelectWallet, replyMarkup: keyboard);
    }


    private async Task WalletMenu(UpdateData data, Guid walletId)
    {
        var wallet = await tServices.GetWalletForUser(walletId, data.ChatId);
        if (wallet == null) { await services.SendMessage(data.ChatId, Messages.WalletNotFound); return; }
        List<List<Tuple<string, string>>> collection = 
        [
            [new(Messages.KeyboardInviteToWallet, $"{CallBacks.InviteToWallet}\\{walletId}")],
            [new(Messages.KeyboardManageCategories, $"{CallBacks.ManageCategories}\\{walletId}")],
            [new(Messages.KeyboardAddTransaction, $"{CallBacks.AddTransaction}\\{walletId}")]
        ];

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}\\");
        await services.SendMessage(data.ChatId, $"Wallet: {wallet.Name}", replyMarkup: keyboard);
    }

    private async Task InviteWallet(long chatId, Guid walletId)
    {
        var wallet = await tServices.GetWalletForUser(walletId, chatId);
        if (wallet == null) { await services.SendMessage(chatId, Messages.WalletNotFound); return; }
        var link = $"{services.Url}?start={CallBacks.Transaction}_{CallBacks.JoinWallet}_{walletId}";
        await services.SendMessage(chatId, string.Format(Messages.InviteToWallet, wallet.Name, link));
    }

    private async Task CategoryMenu(UpdateData data, Guid walletId)
    {
        var wallet = await tServices.GetWalletForUser(walletId, data.ChatId);
        if (wallet == null) { await services.SendMessage(data.ChatId, Messages.WalletNotFound); return; }
        var categories = await tServices.GetCategories(walletId);
        var rows = categories.Select(c => new List<Tuple<string, string>> { new(c.Name!, $"{CallBacks.DeleteCategory}\\{walletId}\\{c.Id}") }).ToList();
        rows.Add([new(Messages.KeyboardAddCategory, $"{CallBacks.AddCategory}\\{walletId}")]);
        await services.SendMessage(data.ChatId, string.Format(Messages.CategoryMenu, wallet.Name, categories.Count == 0 ? "(none)" : string.Join(", ", categories.Select(c => c.Name))), replyMarkup: services.CreateKeyboard(inlineCollection: rows, callBackStart: $"{CallBacks.Transaction}\\"));
    }

    private async Task AddTransactionMenu(long chatId, Guid walletId)
    {        
        List<List<Tuple<string, string>>> rows = 
        [
            [new(Messages.KeyboardManualTransaction, $"{CallBacks.ManualTransaction}\\{walletId}")],
            [new(Messages.KeyboardBluTransaction, $"{CallBacks.BluTransaction}\\{walletId}")]
        ];

        await services.SendMessage(chatId, Messages.KeyboardAddTransaction, replyMarkup: services.CreateKeyboard(inlineCollection: rows, callBackStart: $"{CallBacks.Transaction}\\"));
    }

    private async Task SelectCategory(long chatId, Guid walletId, string next)
    {
        var categories = await tServices.GetCategories(walletId);
        var rows = categories.Select(c => new List<Tuple<string, string>> { new(c.Name!, $"{next}\\{walletId}\\{c.Id}") }).ToList();
        await services.SendMessage(chatId, Messages.SelectCategory, replyMarkup: services.CreateKeyboard(inlineCollection: rows, callBackStart: $"{CallBacks.Transaction}\\"));
    }
    public async Task CreateWallet(UpdateData data)
    {
        var walletName = data.MessageText!;
        var walletId = await tServices.CreateNewWallet(data.ChatId, walletName);
        await services.SendMessage(data.ChatId, string.Format(Messages.WalletCreated, walletName, walletId));
    }

    public async Task CreateCategory(UpdateData data, string walletId)
    {
        await tServices.AddCategory(data.ChatId, Guid.Parse(walletId), data.MessageText!);
        await services.SendMessage(data.ChatId, string.Format(Messages.CategoryCreated, data.MessageText));
    }

    public async Task AddManualTransaction(UpdateData data, string callbackData)
    {
        var p = callbackData.Split('\\');
        var parts = (data.MessageText ?? "").Split('|', 2);
        if (parts.Length != 2 || !decimal.TryParse(parts[0].Trim().TrimStart('+', '-'), out var amount) || (parts[0].Trim()[0] != '+' && parts[0].Trim()[0] != '-')) { await services.SendMessage(data.ChatId, Messages.AskManualTransaction); return; }
        await tServices.AddTransaction(data.ChatId, Guid.Parse(p[0]), Guid.Parse(p[1]), DateTime.Now, parts[0].Trim()[0] == '+', amount, parts[1]);
        sessionService.ClearSession(data.ChatId); await services.SendMessage(data.ChatId, Messages.TransactionSaved);
    }

    private async Task ShowBluRow(long chatId)
    {
        var s = sessionService.GetData(chatId)!; var rows = (List<TransactionProcess>)s.Context["rows"]; var i = (int)s.Context["index"]; if (i >= rows.Count) { sessionService.ClearSession(chatId); await services.SendMessage(chatId, Messages.BluFinished); return; }
        var r = rows[i];
        List<List<Tuple<string, string>>> buttons = 
        [
            [new(Messages.Yes, $"{CallBacks.BluAction}\\{i}\\add"),
                new(Messages.No, $"{CallBacks.BluAction}\\{i}\\ignore")]
        ];

        await services.SendMessage(chatId,
            string.Format(Messages.BluReview, r.Date, r.Deposit > 0 ? r.Deposit : r.Withdraw, r.Description,
                r.Description),
            replyMarkup: services.CreateKeyboard(inlineCollection: buttons,
                callBackStart: $"{CallBacks.Transaction}\\"));
    }

    private async Task BluAction(UpdateData data)
    {
        var s = sessionService.GetData(data.ChatId); if (s == null) return; var rows = (List<TransactionProcess>)s.Context["rows"]; var i = int.Parse(data.DataSeparated[2]); var r = rows[i];
        if (data.DataSeparated.ElementAtOrDefault(3) == "add") { var category = (await tServices.GetCategories(Guid.Parse((string)s.Context["wallet"]))).FirstOrDefault(); if (category != null) await tServices.AddTransaction(data.ChatId, Guid.Parse((string)s.Context["wallet"]), category.Id, r.Date, r.Deposit > 0, r.Deposit > 0 ? r.Deposit : r.Withdraw, r.Description, r.DocumentNo); }
        s.Context["index"] = i + 1; await ShowBluRow(data.ChatId);
    }
    
    public async Task ProcessBluFile(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session == null) return;
        var rows = new List<TransactionProcess>();
        using var workbook = new XLWorkbook(data.Document!.FileAddress);
        var ws = workbook.Worksheet(1);
        for (var row = 12; !ws.Cell(row, 19).IsEmpty(); row++)
            rows.Add(new TransactionProcess
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
        session.Action = Actions.AwaitingBluReview;
        session.Context["rows"] = rows;
        session.Context["wallet"] = session.CallbackData;
        session.Context["index"] = 0;
        await ShowBluRow(data.ChatId);
    }
}
