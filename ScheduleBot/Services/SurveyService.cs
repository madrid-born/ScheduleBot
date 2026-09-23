using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

/// <summary>Survey persistence and authorization. Published questions are immutable.</summary>
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
        Questions =
        [
            new() { Title = "Which activity do you prefer?", Type = SurveyTypes.Choice, Options = ["Hiking", "Cinema", "Board games"] },
            new() { Title = "What would make this event better?", Type = SurveyTypes.Text },
            new() { Title = "How many hours can you stay?", Type = SurveyTypes.Number },
            new() { Title = "Optional quiz: how much is 2 + 2?", Type = SurveyTypes.Number, RightAnswer = "4" }
        ]
    }, JsonOptions);

    public static SurveyDefinition ParseJson(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes) throw new SurveyValidationException("JSON must be at most 256 KB.");
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
        if (string.IsNullOrEmpty(title) || title.Length > 200) throw new SurveyValidationException("Enter a survey title of 1–200 characters.");
        return title;
    }

    public static void ValidateDefinition(SurveyDefinition definition)
    {
        if (definition.Questions == null || definition.Questions.Count is < 1 or > MaxQuestions)
            throw new SurveyValidationException($"A survey must have 1–{MaxQuestions} questions.");
        for (var i = 0; i < definition.Questions.Count; i++)
        {
            var q = definition.Questions[i];
            if (q == null) throw new SurveyValidationException($"Question {i + 1} cannot be null.");
            q.Title = q.Title?.Trim() ?? "";
            q.Type = q.Type?.Trim().ToLowerInvariant() ?? "";
            if (q.Title.Length is < 1 or > 1000) throw new SurveyValidationException($"Question {i + 1}: title must be 1–1000 characters.");
            if (q.Type is not (SurveyTypes.Text or SurveyTypes.Number or SurveyTypes.Choice))
                throw new SurveyValidationException($"Question {i + 1}: type must be text, number, or choice.");
            if (q.Options == null) throw new SurveyValidationException($"Question {i + 1}: options must be an array.");
            q.Options = q.Options.Select(x => x?.Trim() ?? "").ToList();
            if (q.Type == SurveyTypes.Choice)
            {
                if (q.Options.Count is < 2 or > MaxOptions || q.Options.Any(x => x.Length is < 1 or > 100))
                    throw new SurveyValidationException($"Question {i + 1}: provide 2–{MaxOptions} options, each 1–100 characters.");
                if (q.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != q.Options.Count)
                    throw new SurveyValidationException($"Question {i + 1}: options must be unique.");
            }
            else if (q.Options.Count != 0) throw new SurveyValidationException($"Question {i + 1}: only choice questions can have options.");
            if (q.RightAnswer != null)
            {
                q.RightAnswer = NormalizeValue(q.Type, q.RightAnswer);
                if (q.Type == SurveyTypes.Choice && !q.Options.Contains(q.RightAnswer))
                    throw new SurveyValidationException($"Question {i + 1}: rightAnswer must exactly match an option.");
            }
        }
    }

    public static string NormalizeValue(string type, string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 1000) throw new SurveyValidationException("Enter an answer of 1–1000 characters.");
        if (type != SurveyTypes.Number) return value;
        if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            throw new SurveyValidationException("Enter a number using a decimal point, for example 7 or 3.5 (no thousands separators).");
        return number.ToString("G29", CultureInfo.InvariantCulture);
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

    public async Task<Guid> CreateAsync(long chatId, string title, SurveyDefinition definition)
    {
        title = ValidateTitle(title);
        ValidateDefinition(definition);
        var user = await User(chatId);
        var survey = new Survey { Id = Guid.NewGuid(), CreatorId = user.Id, Name = title, InvitationCode = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow };
        db.Survey.Add(survey);
        db.SurveyAccess.Add(new SurveyAccess { Id = Guid.NewGuid(), SurveyId = survey.Id, UserId = user.Id });
        for (var i = 0; i < definition.Questions.Count; i++)
        {
            var source = definition.Questions[i];
            var question = new Question { Id = Guid.NewGuid(), SurveyId = survey.Id, Title = source.Title, Position = i, DataType = source.Type, RightAnswer = source.RightAnswer };
            db.SurveyQuestion.Add(question);
            db.SurveyAnswer.AddRange(source.Options.Select((value, index) => new Answer
            {
                Id = Guid.NewGuid(), QuestionId = question.Id, Position = index, DataType = SurveyTypes.Choice, Value = value
            }));
        }
        // One SaveChanges transaction publishes the complete survey and grants creator access.
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
                db.SurveyQuestion.Count(q => q.SurveyId == s.Id),
                db.SurveyUserAnswer.Count(a => a.SurveyId == s.Id && a.UserId == user.Id))).ToListAsync();
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
                // Two deliveries of the same invitation may arrive concurrently.
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

    public async Task SaveAnswerAsync(long chatId, Guid questionId, Guid? answerId, string? value)
    {
        var question = await db.SurveyQuestion.AsNoTracking().SingleOrDefaultAsync(x => x.Id == questionId)
            ?? throw new SurveyValidationException("Question not found.");
        await GetSurveyAsync(chatId, question.SurveyId);
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
        var response = await db.SurveyUserAnswer.SingleOrDefaultAsync(x => x.UserId == user.Id && x.QuestionId == questionId && x.SurveyId == question.SurveyId);
        var inserted = response == null;
        if (response == null)
        {
            response = new UserAnswer { Id = Guid.NewGuid(), UserId = user.Id, QuestionId = questionId, SurveyId = question.SurveyId };
            db.SurveyUserAnswer.Add(response);
        }
        response.AnswerId = answerId;
        response.Value = value;
        response.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) when (inserted)
        {
            db.Entry(response).State = EntityState.Detached;
            if (!await db.SurveyUserAnswer.AnyAsync(x => x.UserId == user.Id && x.QuestionId == questionId && x.SurveyId == question.SurveyId)) throw;
            // The first response won a concurrent insert. Do not silently replace it.
            throw new SurveyValidationException("An answer was already saved for this question. Reopen it to review or change it.");
        }
    }

    public async Task<SurveyComparison> CompareAsync(long chatId, Guid surveyId, int questionIndex, int page)
    {
        var progress = await GetProgressAsync(chatId, surveyId);
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

    public static bool IsCorrect(Question question, string? value) => question.RightAnswer != null &&
        string.Equals(question.RightAnswer, value?.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class SurveyValidationException(string message) : Exception(message);
public sealed record SurveyListItem(Guid Id, string Name, int Total, int Answered);
public sealed record SurveyPage(List<SurveyListItem> Items, int Page, int Total);
public sealed record SurveyProgress(Survey Survey, List<Question> Questions, List<UserAnswer> Responses);
public sealed record SurveyComparison(SurveyProgress Progress, int QuestionIndex, int Page, int MemberCount,
    List<Models.User> Users, List<UserAnswer> Responses, int AnsweredCount, List<Answer> Options, Dictionary<Guid, int> OptionCounts);
