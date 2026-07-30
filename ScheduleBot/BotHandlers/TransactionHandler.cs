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
        await services.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: keyboard);
    }
    
    public async Task HandleCallBack(UpdateData data)
    {
        var tt = data.DataSeparated[1];
        switch (data.DataSeparated[1])
        {
            case CallBacks.MainSection:
                switch (data.DataSeparated[2])
                {
                    case CallBacks.CreateWallet:
                        await services.SendMessage(data.ChatId, Messages.AskWalletName, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.WalletManagement:
                        await LoadWallets(data.ChatId, CallBacks.WalletManagement);
                        break;
                }
                break;
            case CallBacks.Show:
            {
                // var tryParse = Guid.TryParse(data.DataSeparated[2], out var cartId2);
                // await ShowCarts(data, tryParse ? cartId2 : Guid.Empty);
                break;
            }
        }
    }

    #region Create Wallet
    
    public async Task CreateWallet(UpdateData data)
    {
        var walletName = data.MessageText!;
        var walletId = await tServices.CreateNewWallet(data.ChatId, walletName);
        await services.SendMessage(data.ChatId, string.Format(Messages.WalletCreated, walletName, walletId));
    }
    
    #endregion

    #region Wallet Managemnet
    
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

    #endregion
    
    
    
    public async Task ProcessBluFile(UpdateData data)
    {
        using var workbook = new XLWorkbook(data.Document!.FileAddress);
        var ws = workbook.Worksheet(1);

        var transactionProcesses = new List<TransactionProcess>();
        for (var row = 12;; row++)
        {
            if (ws.Cell(row, 19).IsEmpty()) break;
            var ss = new TransactionProcess
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
            };
            transactionProcesses.Add(ss);
        }

        transactionProcesses.Reverse();
    }
}