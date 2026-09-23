using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

/// <summary>Survey persistence, portable private exports, and authorization.</summary>
public sealed class SurveyService(AppDbContext db)
{
    public const int MaxQuestions = 100;
    public const int MaxOptions = 10;
    public const int MaxJsonBytes = 256 * 1024;
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    public static readonly string SampleJson = JsonSerializer.Serialize(new SurveyDefinition
    {
        Name = "Message perspectives",
        IsPrivate = true,
        Questions =
        [
            new() { Title = "How do you feel about the message?", Type = SurveyTypes.Choice, Options = ["Good", "Neutral", "Bad"], States = ["Giver", "Receiver"] },
            new() { Title = "What did you expect from the conversation?", Type = SurveyTypes.Text },
            new() { Title = "Rate the clarity from 1 to 10", Type = SurveyTypes.Number, States = ["Host", "Guest"] },
            new() { Title = "Optional quiz: how much is 2 + 2?", Type = SurveyTypes.Number, RightAnswer = "4" }
        ]
    }, JsonOptions);

    public static SurveyDefinition ParseJson(string json)
    {
        CheckJsonSize(json);
        SurveyDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<SurveyDefinition>(json, JsonOptions)
                ?? throw new SurveyValidationException("Send a JSON object containing a questions array.");
        }
        catch (JsonException ex)
        {
            throw new SurveyValidationException($"Invalid JSON near {ex.Path ?? "the root"} (line {(ex.LineNumber ?? 0) + 1}). Use the template's field names and types.");
        }
        ValidateDefinition(definition);
        return definition;
    }

    public static string ValidateTitle(string? title)
    {
        title = title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200) throw new SurveyValidationException("Enter a survey title of 1-200 characters.");
        return title;
    }

    public static void ValidateDefinition(SurveyDefinition definition)
    {
        definition.Name = ValidateTitle(definition.Name);
        if (definition.Questions == null || definition.Questions.Count is < 1 or > MaxQuestions)
            throw new SurveyValidationException($"A survey must have 1-{MaxQuestions} questions.");
        for (var i = 0; i < definition.Questions.Count; i++)
        {
            var q = definition.Questions[i];
            if (q == null) throw new SurveyValidationException($"Question {i + 1} cannot be null.");
            q.Title = q.Title?.Trim() ?? "";
            q.Type = q.Type?.Trim().ToLowerInvariant() ?? "";
            if (q.Title.Length is < 1 or > 1000) throw new SurveyValidationException($"Question {i + 1}: title must be 1-1000 characters.");
            if (q.Type is not (SurveyTypes.Text or SurveyTypes.Number or SurveyTypes.Choice))
                throw new SurveyValidationException($"Question {i + 1}: type must be text, number, or choice.");
            if (q.States == null) throw new SurveyValidationException($"Question {i + 1}: states must be an array.");
            q.States = q.States.Select(x => x?.Trim() ?? "").ToList();
            if (q.States.Count is not (0 or 2))
                throw new SurveyValidationException($"Question {i + 1}: states must be empty or contain exactly two names.");
            if (q.States.Any(x => x.Length is < 1 or > 50) || q.States.Distinct(StringComparer.OrdinalIgnoreCase).Count() != q.States.Count)
                throw new SurveyValidationException($"Question {i + 1}: state names must be unique and 1-50 characters each.");
            if (q.Options == null) throw new SurveyValidationException($"Question {i + 1}: options must be an array.");
            q.Options = q.Options.Select(x => x?.Trim() ?? "").ToList();
            if (q.Type == SurveyTypes.Choice)
            {
                if (q.Options.Count is < 2 or > MaxOptions || q.Options.Any(x => x.Length is < 1 or > 100))
                    throw new SurveyValidationException($"Question {i + 1}: provide 2-{MaxOptions} options, each 1-100 characters.");
                if (q.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != q.Options.Count)
                    throw new SurveyValidationException($"Question {i + 1}: options must be unique.");
            }
            else if (q.Options.Count != 0) throw new SurveyValidationException($"Question {i + 1}: only choice questions can have options.");
            if (q.RightAnswer == null) continue;
            q.RightAnswer = NormalizeValue(q.Type, q.RightAnswer);
            if (q.Type == SurveyTypes.Choice && !q.Options.Contains(q.RightAnswer))
                throw new SurveyValidationException($"Question {i + 1}: rightAnswer must exactly match an option.");
        }
    }

    public static string NormalizeValue(string type, string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 1000) throw new SurveyValidationException("Enter an answer of 1-1000 characters.");
        if (type != SurveyTypes.Number) return value;
        if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            throw new SurveyValidationException("Enter a number using a decimal point, for example 7 or 3.5 (no thousands separators).");
        return number.ToString("G29", CultureInfo.InvariantCulture);
    }

    private static void CheckJsonSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes) throw new SurveyValidationException("JSON must be at most 256 KB.");
    }

    private async Task<Models.User> User(long chatId) => await db.Users.SingleOrDefaultAsync(x => x.ChatId == chatId)
        ?? throw new SurveyValidationException("Please register with the bot first.");

    private IQueryable<Survey> Accessible(Guid userId) => db.Survey.Where(s => s.CreatorId == userId ||
        db.SurveyAccess.Any(a => a.SurveyId == s.Id && a.UserId == userId));

    public async Task<Survey> GetSurveyAsync(long chatId, Guid surveyId)
    {
        var user = await User(chatId);
        return await Accessible(user.Id).AsNoTracking().SingleOrDefaultAsync(x => x.Id == surveyId)
            ?? throw new SurveyValidationException("Survey not found or you do not have access. Open its invitation link first.");
    }

    public Task<Guid> CreateAsync(long chatId, string title, SurveyDefinition definition)
    {
        definition.Name = title;
        return CreateAsync(chatId, definition);
    }

    public async Task<Guid> CreateAsync(long chatId, SurveyDefinition definition)
    {
        ValidateDefinition(definition);
        var user = await User(chatId);
        var survey = new Survey
        {
            Id = Guid.NewGuid(), CreatorId = user.Id, Name = definition.Name, InvitationCode = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow,
            IsPrivate = definition.IsPrivate
        };
        db.Survey.Add(survey);
        db.SurveyAccess.Add(new SurveyAccess { Id = Guid.NewGuid(), SurveyId = survey.Id, UserId = user.Id });
        for (var i = 0; i < definition.Questions.Count; i++)
        {
            var source = definition.Questions[i];
            var question = new Question
            {
                Id = Guid.NewGuid(), SurveyId = survey.Id, Title = source.Title, Position = i, DataType = source.Type, RightAnswer = source.RightAnswer,
                StateOneName = source.States.ElementAtOrDefault(0), StateTwoName = source.States.ElementAtOrDefault(1)
            };
            db.SurveyQuestion.Add(question);
            db.SurveyAnswer.AddRange(source.Options.Select((value, index) => new Answer
            {
                Id = Guid.NewGuid(), QuestionId = question.Id, Position = index, DataType = SurveyTypes.Choice, Value = value
            }));
        }
        await db.SaveChangesAsync();
        return survey.Id;
    }

    public async Task<SurveyPage> ListAsync(long chatId, bool ownedOnly, int page)
    {
        var user = await User(chatId);
        var query = Accessible(user.Id);
        if (ownedOnly) query = query.Where(x => x.CreatorId == user.Id);
        var count = await query.CountAsync();
        page = Math.Clamp(page, 0, Math.Max(0, (count - 1) / 6));
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page * 6).Take(6)
            .Select(s => new SurveyListItem(s.Id, s.Name,
                db.SurveyQuestion.Where(q => q.SurveyId == s.Id).Sum(q => q.StateOneName == null ? 1 : 2),
                db.SurveyUserAnswer.Count(a => a.SurveyId == s.Id && a.UserId == user.Id), s.IsPrivate)).ToListAsync();
        return new SurveyPage(items, page, count);
    }

    public async Task<Survey> GetInvitationAsync(long chatId, Guid surveyId)
    {
        var survey = await GetSurveyAsync(chatId, surveyId);
        if (survey.CreatorId != (await User(chatId)).Id) throw new SurveyValidationException("Only the survey creator can generate invitations.");
        return survey;
    }

    public async Task<Survey> JoinAsync(long chatId, Guid invitationCode)
    {
        var user = await User(chatId);
        var survey = await db.Survey.AsNoTracking().SingleOrDefaultAsync(x => x.InvitationCode == invitationCode)
            ?? throw new SurveyValidationException("This survey invitation is invalid.");
        if (!await db.SurveyAccess.AnyAsync(x => x.SurveyId == survey.Id && x.UserId == user.Id))
        {
            var access = new SurveyAccess { Id = Guid.NewGuid(), SurveyId = survey.Id, UserId = user.Id };
            db.SurveyAccess.Add(access);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                db.Entry(access).State = EntityState.Detached;
                if (!await db.SurveyAccess.AnyAsync(x => x.SurveyId == survey.Id && x.UserId == user.Id)) throw;
            }
        }
        return survey;
    }

    public async Task<SurveyProgress> GetProgressAsync(long chatId, Guid surveyId)
    {
        var survey = await GetSurveyAsync(chatId, surveyId);
        var user = await User(chatId);
        var questions = await db.SurveyQuestion.AsNoTracking().Where(x => x.SurveyId == surveyId).OrderBy(x => x.Position).ToListAsync();
        var responses = await db.SurveyUserAnswer.AsNoTracking().Where(x => x.SurveyId == surveyId && x.UserId == user.Id).ToListAsync();
        return new SurveyProgress(survey, questions, responses);
    }

    public async Task<List<Answer>> GetOptionsAsync(long chatId, Guid questionId)
    {
        var question = await db.SurveyQuestion.AsNoTracking().SingleOrDefaultAsync(x => x.Id == questionId)
            ?? throw new SurveyValidationException("Question not found.");
        await GetSurveyAsync(chatId, question.SurveyId);
        return await db.SurveyAnswer.AsNoTracking().Where(x => x.QuestionId == questionId).OrderBy(x => x.Position).ToListAsync();
    }

    public async Task SaveAnswerAsync(long chatId, Guid questionId, Guid? answerId, string? value, int stateIndex = 0)
    {
        var question = await db.SurveyQuestion.AsNoTracking().SingleOrDefaultAsync(x => x.Id == questionId)
            ?? throw new SurveyValidationException("Question not found.");
        await GetSurveyAsync(chatId, question.SurveyId);
        if (stateIndex < 0 || stateIndex >= question.StateCount) throw new SurveyValidationException("That answer state does not belong to this question.");
        var user = await User(chatId);
        if (question.DataType == SurveyTypes.Choice)
        {
            var option = await db.SurveyAnswer.AsNoTracking().SingleOrDefaultAsync(x => x.Id == answerId && x.QuestionId == questionId);
            if (option == null) throw new SurveyValidationException("Choose an option from the current question's buttons.");
            value = option.Value;
        }
        else
        {
            if (answerId != null) throw new SurveyValidationException("This question requires a message answer.");
            value = NormalizeValue(question.DataType, value);
        }
        var response = await db.SurveyUserAnswer.SingleOrDefaultAsync(x => x.UserId == user.Id && x.QuestionId == questionId &&
            x.SurveyId == question.SurveyId && x.StateIndex == stateIndex);
        var inserted = response == null;
        if (response == null)
        {
            response = new UserAnswer { Id = Guid.NewGuid(), UserId = user.Id, QuestionId = questionId, SurveyId = question.SurveyId, StateIndex = stateIndex };
            db.SurveyUserAnswer.Add(response);
        }
        response.AnswerId = answerId;
        response.Value = value;
        response.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) when (inserted)
        {
            db.Entry(response).State = EntityState.Detached;
            if (!await db.SurveyUserAnswer.AnyAsync(x => x.UserId == user.Id && x.QuestionId == questionId && x.SurveyId == question.SurveyId && x.StateIndex == stateIndex)) throw;
            throw new SurveyValidationException("An answer was already saved for this question and state. Reopen it to review or change it.");
        }
    }

    public async Task<SurveyComparison> CompareAsync(long chatId, Guid surveyId, int questionIndex, int page)
    {
        var progress = await GetProgressAsync(chatId, surveyId);
        if (progress.Survey.IsPrivate) throw new SurveyValidationException("Private surveys compare only shared JSON answer files.");
        questionIndex = Math.Clamp(questionIndex, 0, progress.Questions.Count - 1);
        var question = progress.Questions[questionIndex];
        var members = db.Users.Where(u => db.SurveyAccess.Any(a => a.SurveyId == surveyId && a.UserId == u.Id));
        var count = await members.CountAsync();
        page = Math.Clamp(page, 0, Math.Max(0, (count - 1) / 6));
        var users = await members.OrderBy(u => u.Name).ThenBy(u => u.Id).Skip(page * 6).Take(6).ToListAsync();
        var ids = users.Select(u => u.Id).ToList();
        var responses = await db.SurveyUserAnswer.AsNoTracking().Where(a => a.SurveyId == surveyId && ids.Contains(a.UserId)).ToListAsync();
        var totalAnswered = await db.SurveyUserAnswer.CountAsync(a => a.QuestionId == question.Id);
        var counts = await db.SurveyUserAnswer.Where(a => a.QuestionId == question.Id && a.AnswerId != null)
            .GroupBy(a => a.AnswerId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        var options = await GetOptionsAsync(chatId, question.Id);
        return new SurveyComparison(progress, questionIndex, page, count, users, responses, totalAnswered, options, counts);
    }

    public async Task<(SurveyAnswerExport Export, string Json)> ExportMineAsync(long chatId, Guid surveyId)
    {
        var progress = await GetProgressAsync(chatId, surveyId);
        var user = await User(chatId);
        var definition = await DefinitionAsync(progress.Survey, progress.Questions);
        var export = new SurveyAnswerExport
        {
            SurveyId = surveyId, SurveyName = progress.Survey.Name, SurveyFingerprint = Fingerprint(definition),
            ParticipantName = user.Name ?? user.Username ?? "Member", ShareId = ParticipantShareId(user.Id, surveyId), ExportedAtUtc = DateTime.UtcNow,
        };
        foreach (var answer in progress.Responses.OrderBy(a => progress.Questions.Find(q => q.Id == a.QuestionId)!.Position).ThenBy(a => a.StateIndex))
        {
            var question = progress.Questions.Single(q => q.Id == answer.QuestionId);
            export.Answers.Add(new SurveyExportAnswer
            {
                Question = question.Position + 1, Title = question.Title, Type = question.DataType,
                State = question.StateNames.ElementAtOrDefault(answer.StateIndex), Value = answer.Value ?? ""
            });
        }
        return (export, JsonSerializer.Serialize(export, JsonOptions));
    }

    public async Task<SurveyAnswerExport> ParseSharedExportAsync(long chatId, Guid surveyId, string json)
    {
        CheckJsonSize(json);
        var survey = await GetSurveyAsync(chatId, surveyId);
        if (!survey.IsPrivate) throw new SurveyValidationException("JSON comparison is only used for private surveys.");
        SurveyAnswerExport export;
        try { export = JsonSerializer.Deserialize<SurveyAnswerExport>(json, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException ex) { throw new SurveyValidationException($"This is not a valid survey answer export{(ex.Path == null ? "." : $" near {ex.Path}.")}"); }
        var questions = await db.SurveyQuestion.AsNoTracking().Where(q => q.SurveyId == surveyId).OrderBy(q => q.Position).ToListAsync();
        var definition = await DefinitionAsync(survey, questions);
        if (export.SchemaVersion != 1 || export.SurveyId != survey.Id || export.SurveyFingerprint != Fingerprint(definition))
            throw new SurveyValidationException("This JSON belongs to a different survey or survey format.");
        if (string.IsNullOrWhiteSpace(export.ParticipantName) || export.ParticipantName.Length > 200 || export.ShareId == Guid.Empty || export.Answers == null)
            throw new SurveyValidationException("The answer export is missing participant information.");
        var seen = new HashSet<(int, string?)>();
        foreach (var answer in export.Answers)
        {
            var question = questions.ElementAtOrDefault(answer.Question - 1);
            if (question == null || question.Title != answer.Title || question.DataType != answer.Type)
                throw new SurveyValidationException($"Exported question {answer.Question} does not match this survey.");
            if ((question.StateCount == 2 && !question.StateNames.Contains(answer.State)) || (question.StateCount == 1 && answer.State != null))
                throw new SurveyValidationException($"Exported question {answer.Question} has an invalid state.");
            if (!seen.Add((answer.Question, answer.State))) throw new SurveyValidationException("The export contains a duplicate answer.");
            _ = NormalizeValue(question.DataType, answer.Value);
            if (question.DataType == SurveyTypes.Choice && !definition.Questions[question.Position].Options.Contains(answer.Value))
                throw new SurveyValidationException($"Exported question {answer.Question} has an invalid choice.");
        }
        return export;
    }

    public async Task<SurveyComparisonData> PublicComparisonDataAsync(long chatId, Guid surveyId)
    {
        var survey = await GetSurveyAsync(chatId, surveyId);
        if (survey.IsPrivate) throw new SurveyValidationException("Private surveys require shared JSON files for comparison.");
        var questions = await db.SurveyQuestion.AsNoTracking().Where(q => q.SurveyId == surveyId).OrderBy(q => q.Position).ToListAsync();
        var definition = await DefinitionAsync(survey, questions);
        var users = await db.Users.AsNoTracking().Where(u => db.SurveyAccess.Any(a => a.SurveyId == surveyId && a.UserId == u.Id))
            .OrderBy(u => u.Name).ThenBy(u => u.Id).ToListAsync();
        var ids = users.Select(u => u.Id).ToList();
        var answers = await db.SurveyUserAnswer.AsNoTracking().Where(a => a.SurveyId == surveyId && ids.Contains(a.UserId)).ToListAsync();
        return new SurveyComparisonData
        {
            SurveyName = survey.Name, Questions = definition.Questions,
            Participants = users.Select(user => new SurveyComparisonParticipant(user.Id.ToString("N"), user.Name ?? user.Username ?? "Member",
                answers.Where(a => a.UserId == user.Id).Select(a => ToExportAnswer(a, questions)).ToList())).ToList()
        };
    }

    public async Task<SurveyComparisonData> PrivateComparisonDataAsync(long chatId, Guid surveyId, IReadOnlyCollection<SurveyAnswerExport> exports)
    {
        var survey = await GetSurveyAsync(chatId, surveyId);
        if (!survey.IsPrivate) throw new SurveyValidationException("This survey uses its public member comparison.");
        if (exports.Count < 2) throw new SurveyValidationException("Upload at least two different answer JSON files before comparing.");
        if (exports.Select(x => x.ShareId).Distinct().Count() != exports.Count)
            throw new SurveyValidationException("The same participant export was supplied more than once.");
        var questions = await db.SurveyQuestion.AsNoTracking().Where(q => q.SurveyId == surveyId).OrderBy(q => q.Position).ToListAsync();
        var definition = await DefinitionAsync(survey, questions);
        return new SurveyComparisonData
        {
            SurveyName = survey.Name, IsPrivate = true, Questions = definition.Questions,
            Participants = exports.Select(e => new SurveyComparisonParticipant(e.ShareId.ToString("N"), e.ParticipantName.Trim(), e.Answers)).ToList()
        };
    }

    private async Task<SurveyDefinition> DefinitionAsync(Survey survey, List<Question> questions)
    {
        var ids = questions.Select(q => q.Id).ToList();
        var options = await db.SurveyAnswer.AsNoTracking().Where(a => ids.Contains(a.QuestionId)).OrderBy(a => a.Position).ToListAsync();
        return new SurveyDefinition
        {
            Name = survey.Name, IsPrivate = survey.IsPrivate,
            Questions = questions.Select(q => new SurveyQuestionDefinition
            {
                Title = q.Title, Type = q.DataType, RightAnswer = q.RightAnswer,
                Options = options.Where(a => a.QuestionId == q.Id).Select(a => a.Value).ToList(), States = [.. q.StateNames]
            }).ToList()
        };
    }

    private static string Fingerprint(SurveyDefinition definition)
    {
        var json = JsonSerializer.Serialize(definition, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static Guid ParticipantShareId(Guid userId, Guid surveyId)
    {
        var input = userId.ToByteArray().Concat(surveyId.ToByteArray()).ToArray();
        return new Guid(SHA256.HashData(input)[..16]);
    }

    private static SurveyExportAnswer ToExportAnswer(UserAnswer answer, List<Question> questions)
    {
        var question = questions.Single(q => q.Id == answer.QuestionId);
        return new SurveyExportAnswer
        {
            Question = question.Position + 1, Title = question.Title, Type = question.DataType,
            State = question.StateNames.ElementAtOrDefault(answer.StateIndex), Value = answer.Value ?? ""
        };
    }

    public static bool IsCorrect(Question question, string? value) => question.RightAnswer != null &&
        string.Equals(question.RightAnswer, value?.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class SurveyValidationException(string message) : Exception(message);
public sealed record SurveyListItem(Guid Id, string Name, int Total, int Answered, bool IsPrivate = false);
public sealed record SurveyPage(List<SurveyListItem> Items, int Page, int Total);
public sealed record SurveyProgress(Survey Survey, List<Question> Questions, List<UserAnswer> Responses);
public sealed record SurveyComparison(SurveyProgress Progress, int QuestionIndex, int Page, int MemberCount,
    List<Models.User> Users, List<UserAnswer> Responses, int AnsweredCount, List<Answer> Options, Dictionary<Guid, int> OptionCounts);
