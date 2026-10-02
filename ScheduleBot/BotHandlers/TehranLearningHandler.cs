using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public sealed class TehranLearningHandler(DatabaseService database, TehranLearningService learning,
    TehranMapCatalog maps, MainService services, ITelegramBotClient bot)
{
    public async Task HandleSection(UpdateData data)
    {
        var user = await database.GetUserByTelId(data.ChatId);
        if (user == null) return;
        var learnt = await learning.GetLearntPlacesAsync(user.Id);
        await services.SendMessage(data.ChatId,
            $"Your Tehran map: {learnt.Count} places learned.\n\nTonight: learn 2 new places, then answer up to 15 different questions. Early nights are shorter—questions never repeat. Unlearned places stay hidden.",
            Menu());
    }

    public static InlineKeyboardMarkup Menu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📖 Learn", "*Tehran|LEARN"),
            InlineKeyboardButton.WithCallbackData("🧠 Quiz", "*Tehran|START"),
            InlineKeyboardButton.WithCallbackData("🗺 Map", "*Tehran|MAP") }
    });

    public async Task HandleCallBack(UpdateData data)
    {
        var user = await database.GetUserByTelId(data.ChatId);
        if (user == null) return;
        switch (data.DataSeparated.ElementAtOrDefault(1))
        {
            case "LEARN":
                await Learn(data.ChatId, user.Id);
                break;
            case "DONE":
                if (!Guid.TryParse(data.DataSeparated.ElementAtOrDefault(2), out var nightId) ||
                    !int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var index)) return;
                if (!await learning.ConfirmLessonAsync(user.Id, nightId, index))
                {
                    await services.SendMessage(data.ChatId, "That lesson was already confirmed or is no longer active. Tap Learn to continue.", Menu());
                    return;
                }
                await Learn(data.ChatId, user.Id);
                break;
            case "MAP":
                var learnt = await learning.GetLearntPlacesAsync(user.Id);
                await SendMap(data.ChatId, learnt, null, null, true,
                    learnt.Count == 0 ? "0 places learned. Your map starts empty—tap Learn to discover your first two places." :
                        $"Your map: {learnt.Count} learned places. Everything else is still hidden.", Menu());
                break;
            case CallBacks.TehranStartQuiz:
                var quiz = await learning.StartQuizAsync(user.Id);
                var question = quiz == null ? null : await learning.GetCurrentQuestionAsync(user.Id, quiz.Id);
                if (question == null) await ExplainNight(data.ChatId, user.Id);
                else await SendQuestion(data.ChatId, user.Id, question);
                break;
            case CallBacks.TehranAnswer:
                if (!Guid.TryParse(data.DataSeparated.ElementAtOrDefault(2), out var optionId)) return;
                var result = await learning.SubmitAnswerAsync(user.Id, optionId);
                if (result == null)
                {
                    await services.SendMessage(data.ChatId, "That question is no longer active. Tap Quiz to resume.", Menu());
                    return;
                }
                if (!result.Accepted)
                {
                    await services.SendMessage(data.ChatId, "You already answered that question. Tap Quiz to resume.", Menu());
                    return;
                }
                // Remove buttons only after the answer has been committed; keep the map visible.
                if (data.MessageId > 0)
                {
                    try
                    {
                        await bot.EditMessageReplyMarkup(data.ChatId, data.MessageId, new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>()));
                    }
                    catch (ApiRequestException ex) when (ex.ErrorCode == 400)
                    {
                        // An old/deleted message cannot be edited. The committed answer still advances.
                    }
                }
                var feedback = result.IsCorrect ? $"Correct — {result.CorrectName}! +10 points." :
                    $"Correct answer: {result.CorrectName}. Take another look at the map.";
                if (result.QuizCompleted)
                    await services.SendMessage(data.ChatId, $"{feedback}\n\nTonight complete! Score: {result.QuizScore}/{result.MaximumScore}. Your two new places are on your Map. Come back next night for two more.", Menu());
                else
                {
                    await services.SendMessage(data.ChatId, feedback);
                    if (result.NextQuestion != null) await SendQuestion(data.ChatId, user.Id, result.NextQuestion);
                }
                break;
            case "MENU":
            case CallBacks.TehranRefresh:
                await HandleSection(data);
                break;
        }
    }

    private async Task ExplainNight(long chatId, Guid userId)
    {
        var night = await learning.GetNightAsync(userId);
        await services.SendMessage(chatId, night == null ? "You have finished the available lessons. Your learned map is still available." :
            night.IsCompleted ? "Tonight is complete! Come back next night for two more places. You can view your Map now." :
            $"Learn and confirm tonight's two places first ({night.LearntCount}/2 confirmed), then your quiz unlocks.", Menu());
    }

    private async Task Learn(long chatId, Guid userId)
    {
        var night = await learning.GetNightAsync(userId);
        if (night == null || night.IsCompleted)
        {
            await ExplainNight(chatId, userId);
            return;
        }
        if (night.LearntCount == 2)
        {
            var quiz = await learning.StartQuizAsync(userId);
            var question = quiz == null ? null : await learning.GetCurrentQuestionAsync(userId, quiz.Id);
            if (question != null) await SendQuestion(chatId, userId, question);
            return;
        }
        var placeId = night.LearntCount == 0 ? night.FirstPlaceId : night.SecondPlaceId;
        var place = (await learning.GetLearningPlacesAsync()).Single(p => p.Id == placeId);
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✓ I've learned this — continue", $"*Tehran|DONE|{night.Id:N}|{night.LearntCount}") },
            Menu().InlineKeyboard.First().ToArray()
        });
        await SendMap(chatId, await learning.GetLearntPlacesAsync(userId), place, null, true,
            $"Tonight's lesson {night.LearntCount + 1}/2\n\n{maps.LearningText(place)}", keyboard);
    }

    private async Task SendQuestion(long chatId, Guid userId, TehranQuestionView question)
    {
        var keyboard = new InlineKeyboardMarkup(question.Options.Select(o => new[]
            { InlineKeyboardButton.WithCallbackData(o.Text, $"*Tehran|ANSWER|{o.Id}") }).Append(new[]
            { InlineKeyboardButton.WithCallbackData("🗺 Map", "*Tehran|MAP") }));
        var prompt = $"Question {question.Position}/{question.TotalQuestions}\n{question.Prompt}";
        if (question.QuestionType == TehranLearningService.MapQuestionType)
            await SendMap(chatId, await learning.GetLearntPlacesAsync(userId), question.Place, null, false, prompt, keyboard);
        else
            await services.SendMessage(chatId, prompt, keyboard);
    }

    private async Task SendMap(long chatId, IReadOnlyList<Place> visiblePlaces, Place? place, Place? reference,
        bool showNames, string caption, InlineKeyboardMarkup keyboard)
    {
        var bytes = maps.RenderPng(visiblePlaces, place, reference, showNames);
        using var stream = new MemoryStream(bytes);
        await bot.SendPhoto(chatId, new InputFileStream(stream, "tehran-map.png"), caption: caption, replyMarkup: keyboard);
    }
}
