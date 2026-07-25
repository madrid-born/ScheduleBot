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
            [new(Messages.KeyboardCreateWallet,   CallBacks.CreateWallet), new(Messages.KeyboardInviteToWallet, CallBacks.InviteToWallet)],
            [new(Messages.KeyboardAddTransaction, CallBacks.AddTransaction)]
        ];
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Transaction}\\{CallBacks.MainSection}\\");
        await services.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: keyboard);
    }
    
    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated[1])
        {
            case CallBacks.MainSection:
                switch (data.DataSeparated[2])
                {
                    case CallBacks.CreateWallet:
                        await services.SendMessage(data.ChatId, Messages.AskWalletName, replyMarkup: new ForceReplyMarkup());
                        break;
                    case CallBacks.InviteToWallet:
                        await LoadWallets(data.ChatId, data.DataSeparated[2]);
                        break;
                    case CallBacks.AddTransaction:
                        // await services.SendMessage(data.ChatId, Messages.AskCartId, replyMarkup: new ForceReplyMarkup());
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

    #region Wallet
    
    public async Task CreateWallet(UpdateData data)
    {
        var walletName = data.MessageText!;
        var walletId = await tServices.CreateNewWallet(data.ChatId, walletName);
        await services.SendMessage(data.ChatId, string.Format(Messages.WalletCreated, walletName, walletId));
    }
    
    private async Task LoadWallets(long dataChatId, string s)
    {
        throw new NotImplementedException();
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