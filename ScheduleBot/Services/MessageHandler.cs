using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ScheduleBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.Services;

public class MessageHandler(
    ITelegramBotClient bot,
    DatabaseService db,
    UserService userService,
    CycleTrackerService cycleTracker,
    IConfiguration configuration)
{
    private readonly long _adminChatId = configuration.GetValue<long>("Telegram:AdminChatId");

    public async Task HandleUpdateAsync(ITelegramBotClient bot1, Update update, CancellationToken ct)
    {
        try
        {
            var updateData = ExtractUpdateDataAsync(update);
            if(!await userService.CheckUserStatusAsync(updateData)) return;
            if (updateData.IsCallback && !string.IsNullOrEmpty(updateData.CallbackData))
            {
                await HandleCallbackAsync(updateData);
            }
            else
            {
                await HandleMessageAsync(updateData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static UpdateData ExtractUpdateDataAsync(Update update)
    {
        var data = new UpdateData();
        
        if (update.CallbackQuery != null)
        {
            data.IsCallback = true;
            data.ChatId = update.CallbackQuery.Message!.Chat.Id;
            data.Username = update.CallbackQuery.Message!.Chat.Username;
            data.MessageId = update.CallbackQuery.Message.MessageId;
            data.CallbackData = update.CallbackQuery.Data;
            data.DataSeparated = (update.CallbackQuery.Data ?? "").Split("\\").ToList();
            if (update.CallbackQuery.Message.Text == null) return data;
            data.MessageText = update.CallbackQuery.Message.Text;
            data.MessageSeparated = (update.CallbackQuery.Message.Text ?? "").Split('"').ToList();
        }
        else if (update.Message != null)
        {
            data.ChatId = update.Message.Chat.Id;
            data.Username = update.Message.Chat.Username;
            data.MessageId = update.Message.MessageId;
            data.MessageText = update.Message.Text;
            data.MessageSeparated = (update.Message.Text ?? "").Split('"').ToList();

            data.Command = update.Message.Text;
            
            if (update.Message.ReplyToMessage == null) return data;
            data.IsReplied = true;
            data.RepliedMessage = update.Message.ReplyToMessage.Text;
            
            data.ReplyMessageSeparated = (update.Message.ReplyToMessage.Text ?? "").Split('\n').ToList();
            if (data.ReplyMessageSeparated.Count == 0) data.ReplyMessageSeparated.Add(update.Message.ReplyToMessage.Text!);

            if (update.Message.ReplyToMessage.Text != "Enter the name of the product") return data;
            data.Command = "AddItem";
            data.ExistedProductName = update.Message.Text;
        }
        
        return data;
    }

    private async Task HandleCallbackAsync(UpdateData data)
    {
        await bot.DeleteMessage(data.ChatId, data.MessageId);

        switch (data.DataSeparated[0])
        {
            case CallBacks.Register:
                await userService.HandleCallBack(data);
                break;
            case CallBacks.Cycle:
                await cycleTracker.HandleCallBack(data);
                break;
        }
    }

    private async Task HandleMessageAsync(UpdateData data)
    {
        var checkReplied = await CheckReplied(data);
        var checkCommand = await CheckCommand(data);
        var checkKeyboard = await CheckKeyboard(data);

        if (!checkReplied && !checkCommand && !checkKeyboard)
        {
            await bot.SendMessage(data.ChatId, Messages.NotFound, replyMarkup: GetMainKeyboard());
        }
    }

    private async Task<bool> CheckReplied(UpdateData data)
    {
        var flag = false;
        if (!data.IsReplied) return flag;
        switch (data.ReplyMessageSeparated[0])
        {
            case Messages.EnterYourName:
                await userService.AskForEmail(data);
                flag = true;
                break;
            case Messages.EnterYourEmail:
                await userService.RegisterUser(data); 
                flag = true;
                break;
            case Messages.SetupTracker:
                await cycleTracker.SaveLastPeriodStart(data);
                flag = true;
                break;
            case Messages.AskForCycleLength:
                await cycleTracker.SaveCycleLength(data); 
                flag = true;
                break;
            case Messages.AskForPeriodLength:
                await cycleTracker.SavePeriodLength(data);
                flag = true;
                break;
            case Messages.AskForCycleId:
                await cycleTracker.JoinToCycleById(data);
                flag = true;
                break;
        }
        return flag;
    }

    private async Task<bool> CheckCommand(UpdateData data)
    {
        var flag = false;
        if (data.MessageText![..1] != "/") return flag;
        switch (data.MessageText)
        {
            case Messages.Start:
                await bot.SendMessage(data.ChatId, Messages.Welcome, replyMarkup: GetMainKeyboard());
                flag = true;
                break;
            // case Messages.SetupPeriod:
            //     await cycleTracker.AskForLastPeriodStart(data);
            //     flag = true;
            //     break;
            // case Messages.EditPeriod:
            //     await cycleTracker.ShowEditMenu(data.ChatId);
            //     flag = true;
            //     break;
        }
        return flag;
    }

    private async Task<bool> CheckKeyboard(UpdateData data)
    {
        var flag = false;
        var keyboardSymbol = data.MessageText![..3];
        switch (keyboardSymbol)
        {
            case Messages.PeriodTrackerSymbol:
                await cycleTracker.HandleSection(data);
                flag = true;
                break;
        }
        return flag;
    }

    public static ReplyKeyboardMarkup GetMainKeyboard()
    {
        return new ReplyKeyboardMarkup
            ([
                [new KeyboardButton(Messages.PeriodTrackerSymbol + Messages.PeriodTracker), new KeyboardButton(Messages.About)],
            ])
            { ResizeKeyboard = true };
    }
}
