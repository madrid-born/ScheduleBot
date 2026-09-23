using System.Text;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

/// <summary>Guided survey creation, invitation links, resumable responses and member comparisons.</summary>
public sealed class SurveyHandler(UserSessionService sessions, MainService main, SurveyService surveys)
{
    public const string SessionAction = "SurveyFlow";
    private const string DraftKey = "SurveyDraft";
    private const string QuestionKey = "SurveyQuestion";
    private const string PromptKey = "SurveyPrompt";

    private sealed class Draft
    {
        public string Token { get; private set; } = "";
        public void RefreshButtons() => Token = Guid.NewGuid().ToString("N")[..8];
        public string Title { get; set; } = "";
        public SurveyDefinition Definition { get; set; } = new();
        public SurveyQuestionDefinition? Pending { get; set; }
    }

    private static InlineKeyboardButton Button(string title, string action) =>
        InlineKeyboardButton.WithCallbackData(title, $"{CallBacks.Survey}|{action}");
    private static InlineKeyboardMarkup Keyboard(params InlineKeyboardButton[][] rows) => new(rows);
    private Task<int> Send(long chatId, string text, InlineKeyboardMarkup? keyboard = null) =>
        main.SendMessage(chatId, text, keyboard, parseMode: ParseMode.None);
    private static InlineKeyboardMarkup HomeButton() => Keyboard([Button("↩ Surveys / pause", "home")]);
    private static InlineKeyboardButton DraftButton(Draft draft, string title, string action) => Button(title, $"b|{draft.Token}|{action}");

    public async Task HandleSection(UpdateData data)
    {
        sessions.ClearSession(data.ChatId);
        await Send(data.ChatId, "📋 Surveys\nCreate a survey, invite people, and compare responses. Answers are saved after each question and visible by name to survey members.",
            Keyboard([Button("1. Create survey", "create")], [Button("2. Invite people to survey", "list|invite|0")],
                [Button("3. Answer / continue a survey", "list|answer|0")], [Button("4. Watch & compare answers", "list|compare|0")]));
    }

    public async Task HandleCallBack(UpdateData data)
    {
        try
        {
            var parts = data.DataSeparated;
            var action = parts.ElementAtOrDefault(1);
            var value = parts.ElementAtOrDefault(2);
            switch (action)
            {
                case "home":
                    await HandleSection(data);
                    break;
                case "create":
                    var session = sessions.SetData(data.ChatId, SessionAction, "title");
                    session.SetContext(DraftKey, new Draft());
                    await Send(data.ChatId, "What is the survey title? (1–200 characters)\nSend /cancel at any time to discard this draft.", HomeButton());
                    break;
                case "list" when value is "invite" or "answer" or "compare":
                    await List(data.ChatId, value, ParseInt(parts.ElementAtOrDefault(3)));
                    break;
                case "invite" when Guid.TryParse(value, out var inviteId):
                    await Invite(data.ChatId, inviteId);
                    break;
                case "answer" when Guid.TryParse(value, out var answerSurvey):
                    await ShowQuestion(data.ChatId, answerSurvey);
                    break;
                case "q" when Guid.TryParse(value, out var questionSurvey):
                    await ShowQuestion(data.ChatId, questionSurvey, ParseInt(parts.ElementAtOrDefault(3)));
                    break;
                case "compare" when Guid.TryParse(value, out var compareSurvey):
                    await Compare(data.ChatId, compareSurvey, ParseInt(parts.ElementAtOrDefault(3)), ParseInt(parts.ElementAtOrDefault(4)));
                    break;
                case "a" when Guid.TryParse(value, out var optionId):
                    await SaveChoice(data, optionId);
                    break;
                case "b":
                    await HandleDraftButton(data, value, parts.ElementAtOrDefault(3));
                    break;
                default:
                    throw new SurveyValidationException("This button is no longer available. Open Surveys to continue.");
            }
        }
        catch (SurveyValidationException ex) { await Send(data.ChatId, ex.Message); }
    }

    public async Task HandleSession(UpdateData data)
    {
        try
        {
            var session = ActiveSession(data.ChatId);
            if (data.MessageText is "/cancel" || data.MessageText == Messages.Cancel)
            {
                await HandleSection(data);
                return;
            }
            if (session.CallbackData == "answer")
            {
                if (data.Document != null) throw new SurveyValidationException("Send your answer as a text message or use the question's buttons.");
                if (data.RepliedMessageId.HasValue && (!session.Context.TryGetValue(PromptKey, out var prompt) || data.RepliedMessageId != (int)prompt))
                    throw new SurveyValidationException("That reply belongs to an older question. Answer the latest question below.");
                var question = (Question)session.Context[QuestionKey];
                await surveys.SaveAnswerAsync(data.ChatId, question.Id, null, data.MessageText);
                await ShowQuestion(data.ChatId, question.SurveyId);
                return;
            }
            var draft = GetDraft(session);
            if (data.Document != null && session.CallbackData != "json")
                throw new SurveyValidationException("A file is only expected after choosing Upload JSON. Please complete the current step or /cancel.");
            switch (session.CallbackData)
            {
                case "title":
                    draft.Title = SurveyService.ValidateTitle(data.MessageText);
                    session.SetCallBack("method");
                    draft.RefreshButtons();
                    await Send(data.ChatId, $"Survey: {draft.Title}\nHow would you like to add questions?",
                        Keyboard([DraftButton(draft, "🤖 Build with bot", "bot"), DraftButton(draft, "📄 Upload JSON", "json")], [Button("Cancel", "home")]));
                    break;
                case "json":
                    var json = data.MessageText ?? "";
                    if (data.Document?.FileAddress is { } path)
                    {
                        if (!string.Equals(Path.GetExtension(data.DocumentName), ".json", StringComparison.OrdinalIgnoreCase))
                            throw new SurveyValidationException("Upload a .json file, or paste the JSON as a message.");
                        if (new FileInfo(path).Length > SurveyService.MaxJsonBytes) throw new SurveyValidationException("JSON must be at most 256 KB.");
                        json = await File.ReadAllTextAsync(path);
                    }
                    draft.Definition = SurveyService.ParseJson(json);
                    await Review(data.ChatId, session, draft);
                    break;
                case "question":
                    var title = data.MessageText?.Trim() ?? "";
                    if (title.Length is < 1 or > 1000) throw new SurveyValidationException("Enter a question of 1–1000 characters.");
                    draft.Pending = new SurveyQuestionDefinition { Title = title };
                    session.SetCallBack("type");
                    draft.RefreshButtons();
                    await Send(data.ChatId, "How should people answer this question?", Keyboard(
                        [DraftButton(draft, "Choose a fixed option", "choice")],
                        [DraftButton(draft, "Text message", "text"), DraftButton(draft, "Number", "number")], [Button("Cancel", "home")]));
                    break;
                case "options":
                    draft.Pending!.Options = (data.MessageText ?? "").Split('\n').Select(x => x.Trim()).ToList();
                    SurveyService.ValidateDefinition(new SurveyDefinition { Questions = [draft.Pending] });
                    await AskRightAnswer(data.ChatId, session, draft);
                    break;
                case "right":
                    draft.Pending!.RightAnswer = data.MessageText;
                    try { SurveyService.ValidateDefinition(new SurveyDefinition { Questions = [draft.Pending] }); }
                    catch { draft.Pending.RightAnswer = null; throw; }
                    await CommitQuestion(data.ChatId, session, draft);
                    break;
                default:
                    throw new SurveyValidationException("Use the buttons from the latest survey prompt, or send /cancel.");
            }
        }
        catch (SurveyValidationException ex) { await Send(data.ChatId, ex.Message); }
    }

    private UserSession ActiveSession(long chatId)
    {
        var session = sessions.GetOrSetData(chatId);
        if (session.Action != SessionAction || session.Timestamp.AddHours(1) < DateTime.UtcNow)
            throw new SurveyValidationException("This step has expired. Open Surveys to continue saved answers or start a new draft.");
        return session;
    }

    private static Draft GetDraft(UserSession session) => session.Context.TryGetValue(DraftKey, out var draft) && draft is Draft value
        ? value : throw new SurveyValidationException("This draft has expired. Create a new survey from the menu.");

    private async Task HandleDraftButton(UpdateData data, string? token, string? action)
    {
        var session = ActiveSession(data.ChatId);
        var draft = GetDraft(session);
        if (draft.Token != token) throw new SurveyValidationException("That button belongs to an older draft. Use the latest prompt.");
        switch (action)
        {
            case "bot" when session.CallbackData == "method":
            case "add" when session.CallbackData == "review":
                if (draft.Definition.Questions.Count >= SurveyService.MaxQuestions) throw new SurveyValidationException("The maximum is 100 questions. Publish this survey or remove the last question.");
                session.SetCallBack("question");
                await Send(data.ChatId, $"Send question {draft.Definition.Questions.Count + 1}.\nEach question is required; participants can pause and return later.", HomeButton());
                break;
            case "json" when session.CallbackData == "method":
                session.SetCallBack("json");
                await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SurveyService.SampleJson)))
                {
                    await main.SendMessage(data.ChatId,
                        "Fill this template, then upload a .json file (up to 256 KB), or paste JSON here. The title you already entered is used.\nTypes: text, number, choice. Choice questions need 2–10 unique options. rightAnswer is optional; omit it or use null for ordinary surveys.\nMaximum: 100 questions. Answers will be visible by name to members.",
                        HomeButton(), document: new InputFileStream(stream, "survey-template.json"), parseMode: ParseMode.None);
                }
                break;
            case SurveyTypes.Text or SurveyTypes.Number or SurveyTypes.Choice when session.CallbackData == "type":
                draft.Pending!.Type = action;
                if (action == SurveyTypes.Choice)
                {
                    session.SetCallBack("options");
                    await Send(data.ChatId, "Send 2–10 fixed answers, one per line (up to 100 characters each).", HomeButton());
                }
                else await AskRightAnswer(data.ChatId, session, draft);
                break;
            case "skip" when session.CallbackData == "right":
                draft.Pending!.RightAnswer = null;
                await CommitQuestion(data.ChatId, session, draft);
                break;
            case "remove" when session.CallbackData == "review":
                if (draft.Definition.Questions.Count > 0) draft.Definition.Questions.RemoveAt(draft.Definition.Questions.Count - 1);
                await Review(data.ChatId, session, draft);
                break;
            case "publish" when session.CallbackData == "review":
                session.SetCallBack("publishing");
                Guid id;
                try { id = await surveys.CreateAsync(data.ChatId, draft.Title, draft.Definition); }
                catch { session.SetCallBack("review"); throw; }
                sessions.ClearSession(data.ChatId);
                await Send(data.ChatId, $"✅ “{draft.Title}” is ready with {draft.Definition.Questions.Count} questions.\nPublished questions are fixed so everyone's responses remain comparable.",
                    Keyboard([Button("Invite people", $"invite|{id:N}")], [Button("Answer survey", $"answer|{id:N}")], [Button("Surveys", "home")]));
                break;
            default:
                throw new SurveyValidationException("This step has already changed. Use the latest survey prompt.");
        }
    }

    private async Task AskRightAnswer(long chatId, UserSession session, Draft draft)
    {
        session.SetCallBack("right");
        draft.RefreshButtons();
        await Send(chatId, "Optional: send the correct answer for quiz scoring, or skip for a regular survey.\nFor a choice question, send the exact option text. Correct answers are shown in comparisons.",
            Keyboard([DraftButton(draft, "Skip — no correct answer", "skip")], [Button("Cancel", "home")]));
    }

    private async Task CommitQuestion(long chatId, UserSession session, Draft draft)
    {
        SurveyService.ValidateDefinition(new SurveyDefinition { Questions = [draft.Pending!] });
        draft.Definition.Questions.Add(draft.Pending!);
        draft.Pending = null;
        await Review(chatId, session, draft);
    }

    private async Task Review(long chatId, UserSession session, Draft draft)
    {
        session.SetCallBack("review");
        draft.RefreshButtons();
        var text = new StringBuilder($"Draft: {draft.Title}\n{draft.Definition.Questions.Count} questions\n\n");
        foreach (var (q, index) in draft.Definition.Questions.Select((q, index) => (q, index)).TakeLast(10))
            text.AppendLine($"{index + 1}. {Short(q.Title, 150)} [{q.Type}{(q.Options.Count > 0 ? $", {q.Options.Count} options" : "")}{(q.RightAnswer != null ? ", scored" : "")}]");
        if (draft.Definition.Questions.Count > 10) text.AppendLine("Showing the last 10 questions.");
        text.Append("\nPublishing makes responses visible by name to survey members. Drafts expire after 1 hour of inactivity.");
        List<InlineKeyboardButton[]> rows = [[DraftButton(draft, "＋ Add question", "add")]];
        if (draft.Definition.Questions.Count > 0)
        {
            rows.Add([DraftButton(draft, "Remove last question", "remove")]);
            rows.Add([DraftButton(draft, "✅ Publish survey", "publish")]);
        }
        rows.Add([Button("Discard draft", "home")]);
        await Send(chatId, text.ToString(), new InlineKeyboardMarkup(rows));
    }

    private async Task List(long chatId, string mode, int page)
    {
        var result = await surveys.ListAsync(chatId, mode == "invite", page);
        sessions.ClearSession(chatId);
        var rows = result.Items.Select(x => new[] { Button($"{Short(x.Name, 50)} ({x.Answered}/{x.Total})", $"{mode}|{x.Id:N}") }).ToList();
        var nav = new List<InlineKeyboardButton>();
        if (result.Page > 0) nav.Add(Button("← Previous", $"list|{mode}|{result.Page - 1}"));
        if ((result.Page + 1) * 6 < result.Total) nav.Add(Button("Next →", $"list|{mode}|{result.Page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());
        rows.Add([Button("Surveys", "home")]);
        await Send(chatId, result.Total == 0 ? (mode == "invite" ? "You have no surveys to invite people to. Create one first." : "No surveys yet. Create one or open an invitation link.") :
            $"Select a survey to {mode}. Progress shows your saved answers.", new InlineKeyboardMarkup(rows));
    }

    private async Task Invite(long chatId, Guid surveyId)
    {
        var survey = await surveys.GetInvitationAsync(chatId, surveyId);
        sessions.ClearSession(chatId);
        var link = $"{main.Url}?start={CallBacks.Survey}_join_{survey.InvitationCode:N}";
        await Send(chatId, $"Forward this message to invite people to “{survey.Name}”.\nMembers can answer and see everyone's named responses. New bot users should register, then open this link again.\n\n{link}",
            Keyboard([InlineKeyboardButton.WithUrl("Join survey", link)], [Button("Surveys", "home")]));
    }

    public async Task JoinSurveyByCode(UpdateData data)
    {
        try
        {
            if (!Guid.TryParse(data.MessageText, out var code)) throw new SurveyValidationException("Invalid survey invitation.");
            var survey = await surveys.JoinAsync(data.ChatId, code);
            sessions.ClearSession(data.ChatId);
            await Send(data.ChatId, $"You have access to “{survey.Name}”. Your answers are visible by name to other members.",
                Keyboard([Button("Answer / continue", $"answer|{survey.Id:N}")], [Button("Watch & compare", $"compare|{survey.Id:N}")]));
        }
        catch (SurveyValidationException ex) { await Send(data.ChatId, ex.Message); }
    }

    private async Task ShowQuestion(long chatId, Guid surveyId, int? index = null)
    {
        var progress = await surveys.GetProgressAsync(chatId, surveyId);
        var answered = progress.Responses.Select(x => x.QuestionId).ToHashSet();
        index ??= progress.Questions.FindIndex(q => !answered.Contains(q.Id));
        if (index < 0)
        {
            sessions.ClearSession(chatId);
            var scored = progress.Questions.Where(q => q.RightAnswer != null).ToList();
            var correct = scored.Count(q => SurveyService.IsCorrect(q, progress.Responses.FirstOrDefault(a => a.QuestionId == q.Id)?.Value));
            await Send(chatId, $"✅ Completed “{progress.Survey.Name}”! All {progress.Questions.Count} answers are saved." +
                (scored.Count > 0 ? $"\nQuiz score: {correct}/{scored.Count}." : ""),
                Keyboard([Button("Review / change my answers", $"q|{surveyId:N}|0")], [Button("Watch & compare", $"compare|{surveyId:N}")], [Button("Surveys", "home")]));
            return;
        }
        index = Math.Clamp(index.Value, 0, progress.Questions.Count - 1);
        var question = progress.Questions[index.Value];
        var session = sessions.SetData(chatId, SessionAction, "answer");
        session.SetContext(QuestionKey, question);
        var existing = progress.Responses.FirstOrDefault(x => x.QuestionId == question.Id);
        var text = $"{progress.Survey.Name}\nQuestion {index + 1}/{progress.Questions.Count} · {answered.Count} saved\n\n{question.Title}\n\n" +
            (question.DataType == SurveyTypes.Choice ? "Choose one option below." : question.DataType == SurveyTypes.Number ? "Send a number (for example 3.5)." : "Send your answer as a message.") +
            (existing != null ? $"\nYour saved answer: {existing.Value}\nAnswer again to change it." : "") + "\nAnswers are visible by name to survey members.";
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var option in await surveys.GetOptionsAsync(chatId, question.Id))
            rows.Add([Button(option.Value, $"a|{option.Id:N}")]);
        var nav = new List<InlineKeyboardButton>();
        if (index > 0) nav.Add(Button("← Previous question", $"q|{surveyId:N}|{index - 1}"));
        if (existing != null && index + 1 < progress.Questions.Count) nav.Add(Button("Next question →", $"q|{surveyId:N}|{index + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());
        rows.Add([Button("Continue / finish", $"answer|{surveyId:N}"), Button("Pause", "home")]);
        var promptId = await Send(chatId, text, new InlineKeyboardMarkup(rows));
        session.SetContext(PromptKey, promptId);
    }

    private async Task SaveChoice(UpdateData data, Guid optionId)
    {
        var session = ActiveSession(data.ChatId);
        if (session.CallbackData != "answer" || !session.Context.TryGetValue(PromptKey, out var prompt) || (int)prompt != data.MessageId)
            throw new SurveyValidationException("That is an older question. Use the latest question's buttons or reopen the survey.");
        var question = (Question)session.Context[QuestionKey];
        await surveys.SaveAnswerAsync(data.ChatId, question.Id, optionId, null);
        await ShowQuestion(data.ChatId, question.SurveyId);
    }

    private async Task Compare(long chatId, Guid surveyId, int questionIndex, int page)
    {
        var comparison = await surveys.CompareAsync(chatId, surveyId, questionIndex, page);
        sessions.ClearSession(chatId);
        var questions = comparison.Progress.Questions;
        var question = questions[comparison.QuestionIndex];
        var text = new StringBuilder($"📊 {comparison.Progress.Survey.Name}\nQuestion {comparison.QuestionIndex + 1}/{questions.Count}\n{Short(question.Title, 500)}\n\nAnswered: {comparison.AnsweredCount}/{comparison.MemberCount} members\n");
        if (question.RightAnswer != null) text.AppendLine($"Correct answer: {Short(question.RightAnswer, 150)}");
        foreach (var option in comparison.Options)
        {
            var votes = comparison.OptionCounts.GetValueOrDefault(option.Id);
            text.AppendLine($"{option.Value}: {votes} ({(comparison.AnsweredCount == 0 ? 0 : 100 * votes / comparison.AnsweredCount)}%)");
        }
        text.AppendLine($"\nMembers {comparison.Page * 6 + 1}–{Math.Min((comparison.Page + 1) * 6, comparison.MemberCount)} of {comparison.MemberCount}:");
        foreach (var user in comparison.Users)
        {
            var responses = comparison.Responses.Where(a => a.UserId == user.Id).ToList();
            var response = responses.FirstOrDefault(a => a.QuestionId == question.Id);
            var mine = comparison.Progress.Responses.FirstOrDefault(a => a.QuestionId == question.Id);
            var label = response == null ? "Not answered yet" : Short(response.Value ?? "", 170);
            if (response != null && question.RightAnswer != null) label += SurveyService.IsCorrect(question, response.Value) ? " ✅" : " ❌";
            if (response != null && mine != null && response.UserId != mine.UserId && string.Equals(response.Value, mine.Value, StringComparison.OrdinalIgnoreCase)) label += " · same as you";
            text.AppendLine($"\n{Short(user.Name ?? user.Username ?? "Member", 50)} ({responses.Count}/{questions.Count}): {label}");
        }
        var needsAttachment = question.Title.Length > 500 || question.RightAnswer?.Length > 150 ||
            comparison.Responses.Any(a => a.QuestionId == question.Id && a.Value?.Length > 170);
        if (needsAttachment) text.AppendLine("\nLong text is shortened here. Full answers for this page are in the attached file.");
        var rows = new List<InlineKeyboardButton[]>();
        var nav = new List<InlineKeyboardButton>();
        if (comparison.QuestionIndex > 0) nav.Add(Button("← Question", $"compare|{surveyId:N}|{comparison.QuestionIndex - 1}|0"));
        if (comparison.QuestionIndex + 1 < questions.Count) nav.Add(Button("Question →", $"compare|{surveyId:N}|{comparison.QuestionIndex + 1}|0"));
        if (nav.Count > 0) rows.Add(nav.ToArray());
        var members = new List<InlineKeyboardButton>();
        if (comparison.Page > 0) members.Add(Button("← Members", $"compare|{surveyId:N}|{comparison.QuestionIndex}|{comparison.Page - 1}"));
        if ((comparison.Page + 1) * 6 < comparison.MemberCount) members.Add(Button("Members →", $"compare|{surveyId:N}|{comparison.QuestionIndex}|{comparison.Page + 1}"));
        if (members.Count > 0) rows.Add(members.ToArray());
        rows.Add([Button("↻ Refresh", $"compare|{surveyId:N}|{comparison.QuestionIndex}|{comparison.Page}"), Button("Surveys", "home")]);
        // Keep the preview below Telegram's limit even with long questions and correct answers.
        var preview = text.ToString();
        if (preview.Length > 3900) preview = Short(preview, 3900);
        await Send(chatId, preview, new InlineKeyboardMarkup(rows));
        if (!needsAttachment) return;
        var full = new StringBuilder($"{comparison.Progress.Survey.Name}\n{question.Title}\n");
        if (question.RightAnswer != null) full.AppendLine($"Correct answer: {question.RightAnswer}");
        foreach (var user in comparison.Users)
            full.AppendLine($"\n{user.Name ?? user.Username ?? "Member"}:\n{comparison.Responses.FirstOrDefault(a => a.UserId == user.Id && a.QuestionId == question.Id)?.Value ?? "Not answered yet"}");
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(full.ToString()));
        await main.SendMessage(chatId, "Full answers for the members on this page.", document: new InputFileStream(stream, "survey-answers.txt"), parseMode: ParseMode.None);
    }

    private static int ParseInt(string? value) => int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : 0;
    private static string Short(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";
}
