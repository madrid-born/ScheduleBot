using System.Text;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

/// <summary>Guided survey creation, resumable responses, private exports, and PDF-first comparisons.</summary>
public sealed class SurveyHandler(UserSessionService sessions, MainService main, SurveyService surveys, SurveyReportService reports)
{
    public const string SessionAction = "SurveyFlow";
    private const string DraftKey = "SurveyDraft";
    private const string QuestionKey = "SurveyQuestion";
    private const string StateKey = "SurveyState";
    private const string PromptKey = "SurveyPrompt";
    private const string SurveyKey = "SurveyId";
    private const string ExportsKey = "SurveyExports";
    private const string CompareDataKey = "SurveyCompareData";

    private sealed class Draft
    {
        public string Token { get; private set; } = "";
        public SurveyDefinition Definition { get; set; } = new();
        public SurveyQuestionDefinition? Pending { get; set; }
        public void RefreshButtons() => Token = Guid.NewGuid().ToString("N")[..8];
    }
    private static InlineKeyboardButton Button(string title, string action) =>
        InlineKeyboardButton.WithCallbackData(title, $"{CallBacks.Survey}|{action}");
    private static InlineKeyboardMarkup Keyboard(params InlineKeyboardButton[][] rows) => new(rows);
    private Task<int> Send(long chatId, string text, InlineKeyboardMarkup? keyboard = null) =>
        main.SendMessage(chatId, text, keyboard, parseMode: ParseMode.None);
    private static InlineKeyboardMarkup HomeButton() => Keyboard([Button("Surveys / pause", "home")]);
    private static InlineKeyboardButton DraftButton(Draft draft, string title, string action) => Button(title, $"b|{draft.Token}|{action}");

    public async Task HandleSection(UpdateData data)
    {
        sessions.ClearSession(data.ChatId);
        await Send(data.ChatId, "Surveys\nCreate a survey, invite people, answer at your own pace, export your answers, and compare responses.",
            Keyboard([Button("Create survey", "create")], [Button("Invite people to survey", "list|invite|0")],
                [Button("Answer / continue a survey", "list|answer|0")], [Button("Watch & compare answers", "list|results|0")]));
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
                case "home": await HandleSection(data); break;
                case "create":
                    var session = sessions.SetData(data.ChatId, SessionAction, "method");
                    var draft = new Draft();
                    draft.RefreshButtons();
                    session.SetContext(DraftKey, draft);
                    await Send(data.ChatId, "How would you like to create the survey?\nJSON creation imports the survey name, privacy, questions, and question-specific states from the file.",
                        Keyboard([DraftButton(draft, "Build with bot", "bot"), DraftButton(draft, "Upload JSON", "json")], [Button("Cancel", "home")]));
                    break;
                case "list" when value is "invite" or "answer" or "results":
                    await List(data.ChatId, value, ParseInt(parts.ElementAtOrDefault(3)));
                    break;
                case "invite" when Guid.TryParse(value, out var inviteId): await Invite(data.ChatId, inviteId); break;
                case "answer" when Guid.TryParse(value, out var answerSurvey): await ShowQuestion(data.ChatId, answerSurvey); break;
                case "q" when Guid.TryParse(value, out var questionSurvey): await ShowQuestion(data.ChatId, questionSurvey, ParseInt(parts.ElementAtOrDefault(3))); break;
                case "results" when Guid.TryParse(value, out var resultsSurvey): await ResultsMenu(data.ChatId, resultsSurvey); break;
                case "watch" when Guid.TryParse(value, out var watchSurvey): await Watch(data.ChatId, watchSurvey); break;
                case "compare" when Guid.TryParse(value, out var compareSurvey): await StartComparison(data.ChatId, compareSurvey); break;
                case "cp" when Guid.TryParse(value, out var publicSurvey):
                    await PublicComparisonPage(data.ChatId, publicSurvey, ParseInt(parts.ElementAtOrDefault(3)), ParseInt(parts.ElementAtOrDefault(4)));
                    break;
                case "private" when Guid.TryParse(value, out var privateSurvey): await BeginPrivateComparison(data.ChatId, privateSurvey); break;
                case "pcdone": await FinishPrivateComparison(data.ChatId, value); break;
                case "pn": await PrivateComparisonPage(data.ChatId, value, ParseInt(parts.ElementAtOrDefault(3)), ParseInt(parts.ElementAtOrDefault(4))); break;
                case "a" when Guid.TryParse(value, out var optionId): await SaveChoice(data, optionId); break;
                case "b": await HandleDraftButton(data, value, parts.ElementAtOrDefault(3)); break;
                default: throw new SurveyValidationException("This button is no longer available. Open Surveys to continue.");
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
                var stateIndex = (int)session.Context[StateKey];
                await surveys.SaveAnswerAsync(data.ChatId, question.Id, null, data.MessageText, stateIndex);
                await ShowQuestion(data.ChatId, question.SurveyId);
                return;
            }
            if (session.CallbackData == "private_json")
            {
                await ReceivePrivateExport(data, session);
                return;
            }
            var draft = GetDraft(session);
            if (data.Document != null && session.CallbackData != "json")
                throw new SurveyValidationException("A file is only expected after choosing Upload JSON. Please complete the current step or /cancel.");
            switch (session.CallbackData)
            {
                case "title":
                    draft.Definition.Name = SurveyService.ValidateTitle(data.MessageText);
                    session.SetCallBack("privacy");
                    draft.RefreshButtons();
                    await Send(data.ChatId, $"Survey: {draft.Definition.Name}\nWho can see responses inside the bot?\nPublic: members can compare member answers.\nPrivate: comparisons use only JSON files people choose to share.",
                        Keyboard([DraftButton(draft, "Public survey", "public"), DraftButton(draft, "Private survey", "private")], [Button("Cancel", "home")]));
                    break;
                case "json":
                    var json = await ReadJson(data, "Upload a .json file, or paste the JSON as a message.");
                    draft.Definition = SurveyService.ParseJson(json);
                    await Review(data.ChatId, session, draft);
                    break;
                case "question_states":
                    draft.Pending!.States = (data.MessageText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    ValidateQuestion(draft.Pending);
                    await AskQuestionType(data.ChatId, session, draft);
                    break;
                case "question":
                    var title = data.MessageText?.Trim() ?? "";
                    if (title.Length is < 1 or > 1000) throw new SurveyValidationException("Enter a question of 1-1000 characters.");
                    draft.Pending = new SurveyQuestionDefinition { Title = title };
                    session.SetCallBack("question_state_mode");
                    draft.RefreshButtons();
                    await Send(data.ChatId, "Should this question have one answer or two perspectives?\nOther questions can use different state names.",
                        Keyboard([DraftButton(draft, "One answer", "qone")], [DraftButton(draft, "Two custom states", "qtwo")], [Button("Cancel", "home")]));
                    break;
                case "options":
                    draft.Pending!.Options = (data.MessageText ?? "").Split('\n').Select(x => x.Trim()).ToList();
                    ValidateQuestion(draft.Pending);
                    await AskRightAnswer(data.ChatId, session, draft);
                    break;
                case "right":
                    draft.Pending!.RightAnswer = data.MessageText;
                    try { ValidateQuestion(draft.Pending); }
                    catch { draft.Pending.RightAnswer = null; throw; }
                    await CommitQuestion(data.ChatId, session, draft);
                    break;
                default: throw new SurveyValidationException("Use the buttons from the latest survey prompt, or send /cancel.");
            }
        }
        catch (SurveyValidationException ex) { await Send(data.ChatId, ex.Message); }
    }

    private UserSession ActiveSession(long chatId)
    {
        var session = sessions.GetOrSetData(chatId);
        if (session.Action != SessionAction || session.Timestamp.AddHours(1) < DateTime.UtcNow)
            throw new SurveyValidationException("This step has expired. Open Surveys to continue saved answers or start again.");
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
                session.SetCallBack("title");
                await Send(data.ChatId, "What is the survey title? (1-200 characters)", HomeButton());
                break;
            case "json" when session.CallbackData == "method":
                session.SetCallBack("json");
                await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SurveyService.SampleJson)))
                {
                    await main.SendMessage(data.ChatId,
                        "Fill this template, then upload a .json file (up to 256 KB), or paste JSON here. The file supplies name and isPrivate. Each question has its own states: [] for one answer or exactly two custom names. Types: text, number, choice.",
                        HomeButton(), document: new InputFileStream(stream, "survey-template.json"), parseMode: ParseMode.None);
                }
                break;
            case "public" or "private" when session.CallbackData == "privacy":
                draft.Definition.IsPrivate = action == "private";
                await StartQuestion(data.ChatId, session, draft);
                break;
            case "qone" when session.CallbackData == "question_state_mode":
                draft.Pending!.States = [];
                await AskQuestionType(data.ChatId, session, draft);
                break;
            case "qtwo" when session.CallbackData == "question_state_mode":
                session.SetCallBack("question_states");
                await Send(data.ChatId, "Send exactly two state names for this question, one per line.\nExample:\nGiver\nReceiver", HomeButton());
                break;
            case "add" when session.CallbackData == "review": await StartQuestion(data.ChatId, session, draft); break;
            case SurveyTypes.Text or SurveyTypes.Number or SurveyTypes.Choice when session.CallbackData == "type":
                draft.Pending!.Type = action;
                if (action == SurveyTypes.Choice)
                {
                    session.SetCallBack("options");
                    await Send(data.ChatId, "Send 2-10 fixed answers, one per line (up to 100 characters each).", HomeButton());
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
                try { id = await surveys.CreateAsync(data.ChatId, draft.Definition); }
                catch { session.SetCallBack("review"); throw; }
                sessions.ClearSession(data.ChatId);
                await Send(data.ChatId, $"Survey ready: {draft.Definition.Name}\n{draft.Definition.Questions.Count} questions, {(draft.Definition.IsPrivate ? "private JSON comparison" : "public member comparison")}.",
                    Keyboard([Button("Invite people", $"invite|{id:N}")], [Button("Answer survey", $"answer|{id:N}")], [Button("Surveys", "home")]));
                break;
            default: throw new SurveyValidationException("This step has already changed. Use the latest survey prompt.");
        }
    }

    private async Task StartQuestion(long chatId, UserSession session, Draft draft)
    {
        if (draft.Definition.Questions.Count >= SurveyService.MaxQuestions) throw new SurveyValidationException("The maximum is 100 questions.");
        session.SetCallBack("question");
        await Send(chatId, $"Send question {draft.Definition.Questions.Count + 1}.\nEvery question is required; participants can pause and return later.", HomeButton());
    }

    private static void ValidateQuestion(SurveyQuestionDefinition question) =>
        SurveyService.ValidateDefinition(new SurveyDefinition { Name = "Temporary", Questions = [question] });

    private async Task AskQuestionType(long chatId, UserSession session, Draft draft)
    {
        session.SetCallBack("type");
        draft.RefreshButtons();
        await Send(chatId, "How should people answer this question?", Keyboard(
            [DraftButton(draft, "Choose a fixed option", "choice")],
            [DraftButton(draft, "Text message", "text"), DraftButton(draft, "Number", "number")], [Button("Cancel", "home")]));
    }

    private async Task AskRightAnswer(long chatId, UserSession session, Draft draft)
    {
        session.SetCallBack("right");
        draft.RefreshButtons();
        await Send(chatId, "Optional: send the correct answer for quiz scoring, or skip for a regular survey.\nFor a choice question, send the exact option text.",
            Keyboard([DraftButton(draft, "Skip - no correct answer", "skip")], [Button("Cancel", "home")]));
    }

    private async Task CommitQuestion(long chatId, UserSession session, Draft draft)
    {
        ValidateQuestion(draft.Pending!);
        draft.Definition.Questions.Add(draft.Pending!);
        draft.Pending = null;
        await Review(chatId, session, draft);
    }

    private async Task Review(long chatId, UserSession session, Draft draft)
    {
        session.SetCallBack("review");
        draft.RefreshButtons();
        var text = new StringBuilder($"Draft: {draft.Definition.Name}\n{(draft.Definition.IsPrivate ? "Private" : "Public")}\n{draft.Definition.Questions.Count} questions\n\n");
        foreach (var (q, index) in draft.Definition.Questions.Select((q, index) => (q, index)).TakeLast(10))
            text.AppendLine($"{index + 1}. {Short(q.Title, 150)} [{q.Type}{(q.States.Count == 2 ? $", {q.States[0]} / {q.States[1]}" : ", one answer")}{(q.Options.Count > 0 ? $", {q.Options.Count} options" : "")}{(q.RightAnswer != null ? ", scored" : "")}]");
        if (draft.Definition.Questions.Count > 10) text.AppendLine("Showing the last 10 questions.");
        text.Append("\nPublished questions and visibility cannot be changed.");
        List<InlineKeyboardButton[]> rows = [[DraftButton(draft, "Add question", "add")]];
        if (draft.Definition.Questions.Count > 0)
        {
            rows.Add([DraftButton(draft, "Remove last question", "remove")]);
            rows.Add([DraftButton(draft, "Publish survey", "publish")]);
        }
        rows.Add([Button("Discard draft", "home")]);
        await Send(chatId, text.ToString(), new InlineKeyboardMarkup(rows));
    }

    private async Task List(long chatId, string mode, int page)
    {
        var result = await surveys.ListAsync(chatId, mode == "invite", page);
        sessions.ClearSession(chatId);
        var rows = result.Items.Select(x => new[] { Button($"{(x.IsPrivate ? "Private | " : "")}{Short(x.Name, 42)} ({x.Answered}/{x.Total})", $"{mode}|{x.Id:N}") }).ToList();
        var nav = new List<InlineKeyboardButton>();
        if (result.Page > 0) nav.Add(Button("Previous", $"list|{mode}|{result.Page - 1}"));
        if ((result.Page + 1) * 6 < result.Total) nav.Add(Button("Next", $"list|{mode}|{result.Page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());
        rows.Add([Button("Surveys", "home")]);
        await Send(chatId, result.Total == 0 ? (mode == "invite" ? "You have no surveys to invite people to." : "No surveys yet. Create one or open an invitation link.") :
            $"Select a survey to {(mode == "results" ? "watch or compare" : mode)}. Progress counts each state separately.", new InlineKeyboardMarkup(rows));
    }

    private async Task Invite(long chatId, Guid surveyId)
    {
        var survey = await surveys.GetInvitationAsync(chatId, surveyId);
        sessions.ClearSession(chatId);
        var link = $"{main.Url}?start={CallBacks.Survey}_join_{survey.InvitationCode:N}";
        var privacy = survey.IsPrivate
            ? "Answers stay private in the bot. Members share their own JSON export when they choose to compare."
            : "Members can compare all named member answers.";
        await Send(chatId, $"Forward this message to invite people to {survey.Name}.\n{privacy}\nNew bot users should register, then open this link again.\n\n{link}",
            Keyboard([InlineKeyboardButton.WithUrl("Join survey", link)], [Button("Surveys", "home")]));
    }

    public async Task JoinSurveyByCode(UpdateData data)
    {
        try
        {
            if (!Guid.TryParse(data.MessageText, out var code)) throw new SurveyValidationException("Invalid survey invitation.");
            var survey = await surveys.JoinAsync(data.ChatId, code);
            sessions.ClearSession(data.ChatId);
            var visibility = survey.IsPrivate
                ? "Your answers remain private until you share your JSON export."
                : "Your answers are visible by name to other members.";
            await Send(data.ChatId, $"You have access to {survey.Name}. {visibility}",
                Keyboard([Button("Answer / continue", $"answer|{survey.Id:N}")], [Button("Watch & compare", $"results|{survey.Id:N}")]));
        }
        catch (SurveyValidationException ex) { await Send(data.ChatId, ex.Message); }
    }

    private async Task ShowQuestion(long chatId, Guid surveyId, int? flatIndex = null)
    {
        var progress = await surveys.GetProgressAsync(chatId, surveyId);
        var slots = progress.Questions.SelectMany(question => Enumerable.Range(0, question.StateCount)
            .Select(stateIndex => (Question: question, StateIndex: stateIndex))).ToList();
        var total = slots.Count;
        var answered = progress.Responses.Select(x => (x.QuestionId, x.StateIndex)).ToHashSet();
        flatIndex ??= Enumerable.Range(0, total).FirstOrDefault(i => !answered.Contains((slots[i].Question.Id, slots[i].StateIndex)), -1);
        if (flatIndex < 0)
        {
            sessions.ClearSession(chatId);
            var scored = progress.Questions.Where(q => q.RightAnswer != null).ToList();
            var correct = progress.Responses.Count(a => scored.Any(q => q.Id == a.QuestionId && SurveyService.IsCorrect(q, a.Value)));
            var scoredTotal = scored.Sum(q => q.StateCount);
            await Send(chatId, $"Completed {progress.Survey.Name}. All {total} answers are saved." + (scoredTotal > 0 ? $"\nQuiz score: {correct}/{scoredTotal}." : ""),
                Keyboard([Button("Review / change my answers", $"q|{surveyId:N}|0")], [Button("Watch & compare", $"results|{surveyId:N}")], [Button("Surveys", "home")]));
            return;
        }
        flatIndex = Math.Clamp(flatIndex.Value, 0, total - 1);
        var question = slots[flatIndex.Value].Question;
        var stateIndex = slots[flatIndex.Value].StateIndex;
        var state = question.StateNames.ElementAtOrDefault(stateIndex);
        var session = sessions.SetData(chatId, SessionAction, "answer");
        session.SetContext(QuestionKey, question);
        session.SetContext(StateKey, stateIndex);
        var existing = progress.Responses.FirstOrDefault(x => x.QuestionId == question.Id && x.StateIndex == stateIndex);
        var privacy = progress.Survey.IsPrivate ? "Only your JSON export can reveal this answer." : "This answer is visible by name to survey members.";
        var text = $"{progress.Survey.Name}\nAnswer {flatIndex + 1}/{total} | {answered.Count} saved" +
            (state == null ? "" : $"\nPerspective: {state}") + $"\n\n{question.Title}\n\n" +
            (question.DataType == SurveyTypes.Choice ? "Choose one option below." : question.DataType == SurveyTypes.Number ? "Send a number (for example 3.5)." : "Send your answer as a message.") +
            (existing != null ? $"\nYour saved answer: {existing.Value}\nAnswer again to change it." : "") + $"\n{privacy}";
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var option in await surveys.GetOptionsAsync(chatId, question.Id)) rows.Add([Button(option.Value, $"a|{option.Id:N}")]);
        var nav = new List<InlineKeyboardButton>();
        if (flatIndex > 0) nav.Add(Button("Previous", $"q|{surveyId:N}|{flatIndex - 1}"));
        if (existing != null && flatIndex + 1 < total) nav.Add(Button("Next", $"q|{surveyId:N}|{flatIndex + 1}"));
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
        await surveys.SaveAnswerAsync(data.ChatId, question.Id, optionId, null, (int)session.Context[StateKey]);
        await ShowQuestion(data.ChatId, question.SurveyId);
    }

    private async Task ResultsMenu(long chatId, Guid surveyId)
    {
        var survey = await surveys.GetSurveyAsync(chatId, surveyId);
        sessions.ClearSession(chatId);
        var compareText = survey.IsPrivate ? "Compare shared JSON files" : "Compare member answers";
        await Send(chatId, $"{survey.Name}\n{(survey.IsPrivate ? "Private survey: the bot will not load other members' stored answers." : "Public survey: comparison uses current member answers.")}",
            Keyboard([Button("Watch / export my answers", $"watch|{surveyId:N}")],
                [Button(compareText, $"{(survey.IsPrivate ? "private" : "compare")}|{surveyId:N}")], [Button("Surveys", "home")]));
    }

    private async Task Watch(long chatId, Guid surveyId)
    {
        var (export, json) = await surveys.ExportMineAsync(chatId, surveyId);
        var summary = new StringBuilder($"Your answers for {export.SurveyName}\n");
        if (export.Answers.Count == 0) summary.Append("\nNo answers saved yet.");
        foreach (var answer in export.Answers)
            summary.Append($"\nQ{answer.Question}{(answer.State == null ? "" : $" [{answer.State}]")}: {Short(answer.Title, 140)}\nAnswer: {Short(answer.Value, 400)}\n");
        summary.Append("\nThe JSON document below contains only your answers. Share it only with people you want to compare with.");
        await Send(chatId, Short(summary.ToString(), 3900), Keyboard([Button("Back", $"results|{surveyId:N}")]));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await main.SendMessage(chatId, "Your shareable survey answers.", document: new InputFileStream(stream, $"survey-{surveyId:N}-answers.json"), parseMode: ParseMode.None);
    }

    private async Task StartComparison(long chatId, Guid surveyId)
    {
        var data = await surveys.PublicComparisonDataAsync(chatId, surveyId);
        await SendPdf(chatId, data);
        await ShowComparisonTelegram(chatId, data, 0, 0, $"cp|{surveyId:N}");
    }

    private async Task PublicComparisonPage(long chatId, Guid surveyId, int question, int cursor)
    {
        var data = await surveys.PublicComparisonDataAsync(chatId, surveyId);
        await ShowComparisonTelegram(chatId, data, question, cursor, $"cp|{surveyId:N}");
    }

    private async Task BeginPrivateComparison(long chatId, Guid surveyId)
    {
        var survey = await surveys.GetSurveyAsync(chatId, surveyId);
        if (!survey.IsPrivate) throw new SurveyValidationException("This survey uses public member comparison.");
        var session = sessions.SetData(chatId, SessionAction, "private_json");
        session.SetContext(SurveyKey, surveyId);
        session.SetContext(ExportsKey, new List<SurveyAnswerExport>());
        var token = Guid.NewGuid().ToString("N")[..8];
        session.SetContext("CompareToken", token);
        await Send(chatId, "Upload answer JSON files one at a time. You may send as many different exports as you want.\nWhen at least two are loaded, press Compare now. The PDF will be sent first, followed by the Telegram comparison.",
            Keyboard([Button("Compare now", $"pcdone|{token}")], [Button("Cancel", "home")]));
    }

    private async Task ReceivePrivateExport(UpdateData data, UserSession session)
    {
        if (data.Document == null || !string.Equals(Path.GetExtension(data.DocumentName), ".json", StringComparison.OrdinalIgnoreCase))
            throw new SurveyValidationException("Upload a .json file created by Watch / export my answers.");
        var json = await ReadJson(data, "Upload a survey answer .json file.");
        var surveyId = (Guid)session.Context[SurveyKey];
        var export = await surveys.ParseSharedExportAsync(data.ChatId, surveyId, json);
        var exports = (List<SurveyAnswerExport>)session.Context[ExportsKey];
        if (exports.Any(x => x.ShareId == export.ShareId)) throw new SurveyValidationException("That exact export is already loaded.");
        exports.Add(export);
        session.SetContext(ExportsKey, exports);
        var token = (string)session.Context["CompareToken"];
        await Send(data.ChatId, $"Loaded {export.ParticipantName}. {exports.Count} JSON file{(exports.Count == 1 ? "" : "s")} ready. Upload another or compare now.",
            Keyboard([Button("Compare now", $"pcdone|{token}")], [Button("Cancel", "home")]));
    }

    private async Task FinishPrivateComparison(long chatId, string? token)
    {
        var session = ActiveSession(chatId);
        if (session.CallbackData != "private_json" || token != (string)session.Context["CompareToken"])
            throw new SurveyValidationException("That comparison session has expired.");
        var data = await surveys.PrivateComparisonDataAsync(chatId, (Guid)session.Context[SurveyKey], (List<SurveyAnswerExport>)session.Context[ExportsKey]);
        session.SetCallBack("private_view");
        session.SetContext(CompareDataKey, data);
        await SendPdf(chatId, data);
        await ShowComparisonTelegram(chatId, data, 0, 0, $"pn|{token}");
    }

    private async Task PrivateComparisonPage(long chatId, string? token, int question, int cursor)
    {
        var session = ActiveSession(chatId);
        if (session.CallbackData != "private_view" || token != (string)session.Context["CompareToken"])
            throw new SurveyValidationException("That private comparison has expired. Upload the JSON files again.");
        await ShowComparisonTelegram(chatId, (SurveyComparisonData)session.Context[CompareDataKey], question, cursor, $"pn|{token}");
    }

    private async Task SendPdf(long chatId, SurveyComparisonData data)
    {
        var pdf = reports.CreateComparisonPdf(data);
        await using var stream = new MemoryStream(pdf);
        await main.SendMessage(chatId, "Complete comparison report. The Telegram view follows next.",
            document: new InputFileStream(stream, "survey-comparison.pdf"), parseMode: ParseMode.None);
    }

    private async Task ShowComparisonTelegram(long chatId, SurveyComparisonData data, int questionIndex, int cursor, string prefix)
    {
        questionIndex = Math.Clamp(questionIndex, 0, data.Questions.Count - 1);
        var question = data.Questions[questionIndex];
        var text = new StringBuilder($"{data.SurveyName}\nQuestion {questionIndex + 1}/{data.Questions.Count}\n{Short(question.Title, 500)}\n\n");
        if (question.Options.Count > 0)
        {
            var all = data.Participants.SelectMany(p => p.Answers).Where(a => a.Question == questionIndex + 1).ToList();
            text.AppendLine("Choice totals: " + string.Join(" | ", question.Options.Select(option =>
            {
                var count = all.Count(a => a.Value == option);
                return $"{option}: {count} ({(all.Count == 0 ? 0 : 100 * count / all.Count)}%)";
            })) + "\n");
        }
        var rows = new List<InlineKeyboardButton[]>();
        if (question.States.Count == 2 && data.Participants.Count > 0)
        {
            cursor = Math.Clamp(cursor, 0, data.Participants.Count - 1);
            var focus = data.Participants[cursor];
            var first = question.States[0];
            var second = question.States[1];
            text.AppendLine($"Focus: {focus.Name} ({cursor + 1}/{data.Participants.Count})");
            foreach (var other in data.Participants.Where(p => p.Key != focus.Key))
            {
                var a = Value(focus, questionIndex + 1, first);
                var b = Value(other, questionIndex + 1, second);
                var c = Value(focus, questionIndex + 1, second);
                var d = Value(other, questionIndex + 1, first);
                text.AppendLine($"\n{focus.Name} [{first}]: {Short(a ?? "Not answered", 110)}");
                text.AppendLine($"vs {other.Name} [{second}]: {Short(b ?? "Not answered", 110)} - {SurveyReportService.CompareNullable(question.Type, a, b)}");
                text.AppendLine($"{focus.Name} [{second}]: {Short(c ?? "Not answered", 110)}");
                text.AppendLine($"vs {other.Name} [{first}]: {Short(d ?? "Not answered", 110)} - {SurveyReportService.CompareNullable(question.Type, c, d)}");
                if (text.Length > 3500) { text.AppendLine("\nMore pairings are in the PDF."); break; }
            }
            var people = new List<InlineKeyboardButton>();
            if (cursor > 0) people.Add(Button("Previous person", $"{prefix}|{questionIndex}|{cursor - 1}"));
            if (cursor + 1 < data.Participants.Count) people.Add(Button("Next person", $"{prefix}|{questionIndex}|{cursor + 1}"));
            if (people.Count > 0) rows.Add(people.ToArray());
        }
        else
        {
            var pageCount = Math.Max(1, (data.Participants.Count + 5) / 6);
            cursor = Math.Clamp(cursor, 0, pageCount - 1);
            var page = data.Participants.Skip(cursor * 6).Take(6).ToList();
            var baseline = data.Participants.Select(p => Value(p, questionIndex + 1, null)).FirstOrDefault(x => x != null);
            foreach (var person in page)
            {
                var value = Value(person, questionIndex + 1, null);
                text.AppendLine($"{person.Name} ({person.Answers.Count}/{data.Questions.Sum(q => q.States.Count == 2 ? 2 : 1)}): {Short(value ?? "Not answered", 300)}" +
                    (value == null || baseline == null ? "" : $" [{SurveyReportService.CompareValues(question.Type, baseline, value)}]") + "\n");
            }
            var pages = new List<InlineKeyboardButton>();
            if (cursor > 0) pages.Add(Button("Previous members", $"{prefix}|{questionIndex}|{cursor - 1}"));
            if (cursor + 1 < pageCount) pages.Add(Button("Next members", $"{prefix}|{questionIndex}|{cursor + 1}"));
            if (pages.Count > 0) rows.Add(pages.ToArray());
        }
        var questions = new List<InlineKeyboardButton>();
        if (questionIndex > 0) questions.Add(Button("Previous question", $"{prefix}|{questionIndex - 1}|0"));
        if (questionIndex + 1 < data.Questions.Count) questions.Add(Button("Next question", $"{prefix}|{questionIndex + 1}|0"));
        if (questions.Count > 0) rows.Add(questions.ToArray());
        rows.Add([Button("Surveys", "home")]);
        await Send(chatId, Short(text.ToString(), 3900), new InlineKeyboardMarkup(rows));
    }

    private static string? Value(SurveyComparisonParticipant person, int question, string? state) =>
        person.Answers.FirstOrDefault(a => a.Question == question && a.State == state)?.Value;

    private static async Task<string> ReadJson(UpdateData data, string wrongFileMessage)
    {
        if (data.Document?.FileAddress is not { } path) return data.MessageText ?? "";
        if (!string.Equals(Path.GetExtension(data.DocumentName), ".json", StringComparison.OrdinalIgnoreCase))
            throw new SurveyValidationException(wrongFileMessage);
        if (new FileInfo(path).Length > SurveyService.MaxJsonBytes) throw new SurveyValidationException("JSON must be at most 256 KB.");
        return await File.ReadAllTextAsync(path);
    }

    private static int ParseInt(string? value) => int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : 0;
    private static string Short(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "...";
}
