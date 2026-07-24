using ClosedXML.Excel;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;

namespace ScheduleBot.BotHandlers;

public class TransactionHandler(ITelegramBotClient bot, IServiceProvider serviceProvider, IConfiguration configuration,
    UserSessionService sessionService, MainService services, TransactionService cServices, ILogger<CycleTrackerHandler> logger)
{
    public async Task HandleSection(UpdateData data)
    {
        // List<List<Tuple<string, string>>> collection = 
        // [
        //     [new(Messages.Edit,        CallBacks.Edit), new(Messages.CurrentStatus, CallBacks.CurrentStatus)],
        //     [new(Messages.AddToCycle,  CallBacks.AddToCycle)],
        //     [new(Messages.JoinToCycle, CallBacks.JoinToCycle)]
        // ];
        //
        // if (await ctServices.GetCycleByTelId(data.ChatId) == null)
        // {
        //     collection = 
        //     [
        //         [new(Messages.Setup,         CallBacks.Setup)],
        //         [new(Messages.JoinToCycle,   CallBacks.JoinToCycle)],
        //         [new(Messages.CurrentStatus, CallBacks.CurrentStatus)],
        //     ];
        // }
        //
        // var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Cycle}\\{CallBacks.MainSection}\\");
        // await services.SendMessage(data.ChatId, Messages.LoadPeriodTracker, replyMarkup: keyboard);
    }
    
    public async Task ProcessBluFile(UpdateData data)
    {
        using var workbook = new XLWorkbook(data.Document!.FileAddress);
        var ws = workbook.Worksheet(1);

        var ss2 = new List<TransactionProcess>();
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
            ss2.Add(ss);
        }

        ss2.Reverse();
    }
}