using System.Data;
using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public sealed record TehranQuestionOptionView(Guid Id, string Text);
public sealed record TehranQuestionView(Guid QuizId, Guid QuestionId, Place Place,
    IReadOnlyList<TehranQuestionOptionView> Options, int Position, int TotalQuestions,
    string QuestionType, string Prompt, Place? ReferencePlace);
public sealed record TehranAnswerResult(bool Accepted, bool IsCorrect, int ScoreAwarded,
    bool QuizCompleted, int QuizScore, string CorrectName, TehranQuestionView? NextQuestion)
{
    public int MaximumScore { get; init; }
}

public sealed class TehranLearningService(AppDbContext db, TehranMapCatalog maps, TimeProvider? clock = null)
{
    public const string MapQuestionType = "identify_highlighted_road";
    public const string OrientationQuestionType = "road_orientation";
    public const string DirectionQuestionType = "relative_direction";
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now.AddHours(3.5));

    public async Task<List<Place>> GetLearningPlacesAsync(CancellationToken ct = default)
    {
        var places = await db.TeaMapPlaces.AsNoTracking().Where(p => p.IsActive)
            .OrderByDescending(p => p.Priority).ThenBy(p => p.Name).ToListAsync(ct);
        return places.Where(maps.CanRender).ToList();
    }

    public async Task<List<Place>> GetLearntPlacesAsync(Guid userId, CancellationToken ct = default)
    {
        var places = await db.TeaMapPlaces.AsNoTracking().Where(p => p.IsActive &&
            db.TeaMapUserPlaceLearningProgress.Any(l => l.UserId == userId && l.PlaceId == p.Id && l.IsLearnt))
            .OrderBy(p => p.Name).ToListAsync(ct);
        return places.Where(maps.CanRender).ToList();
    }

    public Task<TehranLearningNight?> GetNightAsync(Guid userId, CancellationToken ct = default) =>
        InTransaction<TehranLearningNight?>(userId, () => EnsureNight(userId, ct), ct);

    public Task<int> MarkPriorKnowledgeAsync(Guid userId, CancellationToken ct = default) =>
        InTransaction(userId, async () =>
        {
            var requested = new[] { "Valiasr Street", "Shahid Beheshti Street", "North Sohrevardi Ave", "Shariati Street",
                "Shahid Soleimani Expressway", "Hemat Expressway", "Shahid Haghani Expressway", "Sayyad Shirazi Expressway" };
            var catalog = await GetLearningPlacesAsync(ct);
            var places = requested.Select(name => catalog.Single(p => p.Name == name)).ToList();
            foreach (var place in places)
            {
                var progress = await db.TeaMapUserPlaceLearningProgress.SingleOrDefaultAsync(p => p.UserId == userId && p.PlaceId == place.Id, ct);
                if (progress == null)
                {
                    progress = new() { Id = Guid.NewGuid(), UserId = userId, PlaceId = place.Id };
                    db.TeaMapUserPlaceLearningProgress.Add(progress);
                }
                progress.IsLearnt = true;
                progress.LearntAtUtc ??= Now;
                if (progress.Status == TehranLearningStatuses.Unseen) progress.Status = TehranLearningStatuses.Learning;
            }
            await db.SaveChangesAsync(ct);
            return places.Count;
        }, ct);

    private async Task<TehranLearningNight?> EnsureNight(Guid userId, CancellationToken ct)
    {
        // Finish an interrupted night before introducing more new places, even after midnight.
        var night = await db.TeaMapLearningNights.Where(n => n.UserId == userId && (!n.IsCompleted || n.LocalDate == Today))
            .OrderBy(n => n.IsCompleted).ThenBy(n => n.LocalDate).FirstOrDefaultAsync(ct);
        if (night != null) return night;
        var knownIds = await db.TeaMapUserPlaceLearningProgress.Where(p => p.UserId == userId && p.IsLearnt)
            .Select(p => p.PlaceId).ToListAsync(ct);
        var available = (await GetLearningPlacesAsync(ct)).Where(p => !knownIds.Contains(p.Id)).ToList();
        if (available.Count < 2) return null;
        var first = available[0];
        var second = available.Skip(1).Take(30).OrderBy(p => maps.Distance(first, p)).First();
        night = new() { Id = Guid.NewGuid(), UserId = userId, LocalDate = Today,
            FirstPlaceId = first.Id, SecondPlaceId = second.Id };
        db.TeaMapLearningNights.Add(night);
        // Retire the old unrestricted quizzes without deleting historical answers or scores.
        var old = await db.TeaMapQuizzes.Where(q => q.UserId == userId && q.NightId == null && q.Status == TehranQuizStatuses.InProgress).ToListAsync(ct);
        foreach (var quiz in old) { quiz.Status = "abandoned"; quiz.CompletedAtUtc = Now; }
        await db.SaveChangesAsync(ct);
        return night;
    }

    public Task<bool> ConfirmLessonAsync(Guid userId, Guid nightId, int index, CancellationToken ct = default) =>
        InTransaction(userId, async () =>
        {
            var night = await db.TeaMapLearningNights.SingleOrDefaultAsync(n => n.Id == nightId && n.UserId == userId, ct);
            if (night == null || night.IsCompleted || index != night.LearntCount || index is < 0 or > 1) return false;
            var placeId = index == 0 ? night.FirstPlaceId : night.SecondPlaceId;
            var progress = await db.TeaMapUserPlaceLearningProgress.SingleOrDefaultAsync(p => p.UserId == userId && p.PlaceId == placeId, ct);
            if (progress == null)
            {
                progress = new() { Id = Guid.NewGuid(), UserId = userId, PlaceId = placeId };
                db.TeaMapUserPlaceLearningProgress.Add(progress);
            }
            progress.IsLearnt = true;
            progress.LearntAtUtc = Now;
            progress.Status = TehranLearningStatuses.Learning;
            night.LearntCount++;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public async Task<Quiz?> StartQuizAsync(Guid userId, CancellationToken ct = default)
    {
        return await InTransaction<Quiz?>(userId, async () =>
        {
            var night = await EnsureNight(userId, ct);
            if (night == null || night.LearntCount != 2 || night.IsCompleted) return null;
            var existing = await db.TeaMapQuizzes.SingleOrDefaultAsync(q => q.NightId == night.Id, ct);
            if (existing != null)
            {
                await RefreshPendingQuestions(existing, places: await GetLearntPlacesAsync(userId, ct), night, ct);
                return existing;
            }
            var places = await GetLearntPlacesAsync(userId, ct);
            var quiz = new Quiz { Id = Guid.NewGuid(), UserId = userId, NightId = night.Id, StartedAtUtc = Now };
            db.TeaMapQuizzes.Add(quiz);
            var candidates = BuildCandidates(places, night);
            for (var i = 0; i < Math.Min(15, candidates.Count); i++)
            {
                AddQuestion(quiz, candidates[i], places, i);
            }
            await db.SaveChangesAsync(ct);
            return quiz;
        }, ct);
    }

    private sealed record Candidate(Place Place, string Type, Place? Reference = null);
    public static string QuestionKey(string type, Guid placeId, Guid? referenceId)
    {
        // A/B and B/A test the same relationship; do not ask both in one night.
        if (type == DirectionQuestionType && referenceId is { } other)
            return string.CompareOrdinal(placeId.ToString(), other.ToString()) < 0 ? $"{type}:{placeId}:{other}" : $"{type}:{other}:{placeId}";
        return $"{type}:{placeId}";
    }

    private List<Candidate> BuildCandidates(List<Place> places, TehranLearningNight night)
    {
        var ordered = places.OrderBy(p => p.Id == night.FirstPlaceId || p.Id == night.SecondPlaceId ? 0 : 1)
            .ThenBy(_ => Random.Shared.Next()).ToList();
        var names = ordered.Select(p => new Candidate(p, MapQuestionType)).ToList();
        var orientations = ordered.Select(p => new Candidate(p, OrientationQuestionType)).ToList();
        var relations = new List<Candidate>();
        for (var i = 0; i < ordered.Count; i++)
            for (var j = i + 1; j < ordered.Count; j++)
                if (maps.DirectionFrom(ordered[i], ordered[j]) != "Same position")
                    relations.Add(new(ordered[i], DirectionQuestionType, ordered[j]));
        // Recognition is the primary type, with distinct text-only memory questions mixed in.
        var result = new List<Candidate>();
        for (var i = 0; names.Count + orientations.Count + relations.Count > 0; i++)
        {
            var preferred = i % 5 == 3 ? orientations : i % 5 == 4 ? relations : names;
            var pool = preferred.Count > 0 ? preferred : new[] { names, relations, orientations }.First(p => p.Count > 0);
            result.Add(pool[0]); pool.RemoveAt(0);
        }
        return result;
    }

    private void AddQuestion(Quiz quiz, Candidate candidate, List<Place> places, int position)
    {
        var place = candidate.Place; var reference = candidate.Reference; var type = candidate.Type;
        var correct = type == OrientationQuestionType ? maps.Orientation(place) :
            type == DirectionQuestionType ? maps.DirectionFrom(place, reference!) : place.Name;
        var diagonals = new[] { "North-west", "North-east", "South-west", "South-east" };
        var choiceTexts = type == OrientationQuestionType ? new[] { "Mostly north–south", "Mostly east–west" } :
            type == DirectionQuestionType ? diagonals.Contains(correct) ? diagonals : new[] { "North", "South", "East", "West" } :
            places.Where(p => p.Id != place.Id).DistinctBy(p => p.Name).OrderBy(_ => Random.Shared.Next()).Take(3)
                .Select(p => p.Name).Append(place.Name).ToArray();
        var question = new QuizQuestion { Id = Guid.NewGuid(), QuizId = quiz.Id, PlaceId = place.Id,
            ReferencePlaceId = reference?.Id, Position = position, QuestionType = type,
            Prompt = type == OrientationQuestionType ? $"Which direction does {place.Name} mostly run?" :
                type == DirectionQuestionType ? $"Where is {place.Name} relative to {reference!.Name}? Compare their overall locations." :
                "What is the name of the street highlighted in red?" };
        db.TeaMapQuizQuestions.Add(question);
        var choices = choiceTexts.OrderBy(_ => Random.Shared.Next()).ToList();
        for (var j = 0; j < choices.Count; j++)
            db.TeaMapQuizQuestionOptions.Add(new QuizQuestionOption { Id = Guid.NewGuid(), QuizQuestionId = question.Id,
                Position = j, Text = choices[j], IsCorrect = choices[j] == correct });
    }

    private async Task RefreshPendingQuestions(Quiz quiz, List<Place> places, TehranLearningNight night, CancellationToken ct)
    {
        var questions = await db.TeaMapQuizQuestions.Where(q => q.QuizId == quiz.Id).OrderBy(q => q.Position).ToListAsync(ct);
        var ids = questions.Select(q => q.Id).ToList();
        var answeredIds = await db.TeaMapQuizAnswers.Where(a => ids.Contains(a.QuizQuestionId)).Select(a => a.QuizQuestionId).ToListAsync(ct);
        var keys = questions.Select(q => QuestionKey(q.QuestionType, q.PlaceId, q.ReferencePlaceId)).ToList();
        if (keys.Distinct().Count() == keys.Count && questions.Where(q => !answeredIds.Contains(q.Id)).All(q =>
            q.QuestionType == MapQuestionType || (!q.Prompt.Contains("red street") && !q.Prompt.Contains("middles")))) return;
        var answered = questions.Where(q => answeredIds.Contains(q.Id)).ToList();
        var pending = questions.Where(q => !answeredIds.Contains(q.Id)).ToList();
        var pendingIds = pending.Select(q => q.Id).ToList();
        db.TeaMapQuizQuestionOptions.RemoveRange(await db.TeaMapQuizQuestionOptions.Where(o => pendingIds.Contains(o.QuizQuestionId)).ToListAsync(ct));
        db.TeaMapQuizQuestions.RemoveRange(pending);
        await db.SaveChangesAsync(ct);
        var used = answered.Select(q => QuestionKey(q.QuestionType, q.PlaceId, q.ReferencePlaceId)).ToHashSet();
        var remaining = BuildCandidates(places, night).Where(c => !used.Contains(QuestionKey(c.Type, c.Place.Id, c.Reference?.Id)))
            .Take(Math.Max(0, 15 - answered.Count)).ToList();
        var position = answered.Count == 0 ? 0 : answered.Max(q => q.Position) + 1;
        foreach (var candidate in remaining) AddQuestion(quiz, candidate, places, position++);
        if (remaining.Count == 0)
        {
            quiz.Status = TehranQuizStatuses.Completed;
            quiz.CompletedAtUtc = Now;
            night.IsCompleted = true;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<TehranQuestionView?> GetCurrentQuestionAsync(Guid userId, Guid quizId, CancellationToken ct = default)
    {
        if (!await db.TeaMapQuizzes.AnyAsync(q => q.Id == quizId && q.UserId == userId &&
                q.Status == TehranQuizStatuses.InProgress && q.NightId != null, ct)) return null;
        var question = await db.TeaMapQuizQuestions.AsNoTracking().Where(q => q.QuizId == quizId &&
                !db.TeaMapQuizAnswers.Any(a => a.QuizQuestionId == q.Id && a.UserId == userId))
            .OrderBy(q => q.Position).FirstOrDefaultAsync(ct);
        if (question == null) return null;
        var place = await db.TeaMapPlaces.AsNoTracking().SingleAsync(p => p.Id == question.PlaceId, ct);
        var options = await db.TeaMapQuizQuestionOptions.AsNoTracking().Where(o => o.QuizQuestionId == question.Id)
            .OrderBy(o => o.Position).Select(o => new TehranQuestionOptionView(o.Id, o.Text)).ToListAsync(ct);
        return new(quizId, question.Id, place, options, question.Position + 1,
            await db.TeaMapQuizQuestions.CountAsync(q => q.QuizId == quizId, ct), question.QuestionType, question.Prompt,
            question.ReferencePlaceId == null ? null : await db.TeaMapPlaces.AsNoTracking().SingleAsync(p => p.Id == question.ReferencePlaceId, ct));
    }

    public Task<TehranAnswerResult?> SubmitAnswerAsync(Guid userId, Guid optionId, CancellationToken ct = default) =>
        InTransaction<TehranAnswerResult?>(userId, async () =>
        {
            var option = await db.TeaMapQuizQuestionOptions.SingleOrDefaultAsync(o => o.Id == optionId, ct);
            if (option == null) return null;
            var question = await db.TeaMapQuizQuestions.SingleAsync(q => q.Id == option.QuizQuestionId, ct);
            var quiz = await db.TeaMapQuizzes.SingleOrDefaultAsync(q => q.Id == question.QuizId && q.UserId == userId, ct);
            if (quiz == null) return null;
            if (quiz.Status == TehranQuizStatuses.InProgress && quiz.NightId != null &&
                await db.TeaMapQuizQuestions.AnyAsync(q => q.QuizId == quiz.Id &&
                    (q.Prompt.Contains("middles") || q.QuestionType == OrientationQuestionType && q.Prompt.Contains("red street")), ct))
            {
                var night = await db.TeaMapLearningNights.SingleAsync(n => n.Id == quiz.NightId, ct);
                await RefreshPendingQuestions(quiz, await GetLearntPlacesAsync(userId, ct), night, ct);
                // Old unanswered buttons were replaced. Ask the user to resume the updated quiz.
                if (!await db.TeaMapQuizQuestions.AnyAsync(q => q.Id == question.Id, ct)) return null;
            }
            var correctName = await db.TeaMapQuizQuestionOptions.Where(o => o.QuizQuestionId == question.Id && o.IsCorrect)
                .Select(o => o.Text).SingleAsync(ct);
            if (await db.TeaMapQuizAnswers.AnyAsync(a => a.QuizQuestionId == question.Id && a.UserId == userId, ct))
                return new(false, option.IsCorrect, 0, quiz.Status == TehranQuizStatuses.Completed,
                    quiz.Score, correctName, null);
            var current = await GetCurrentQuestionAsync(userId, quiz.Id, ct);
            if (current?.QuestionId != question.Id) return null;
            var now = Now;
            var points = option.IsCorrect ? 10 : 0;
            db.TeaMapQuizAnswers.Add(new QuizAnswer { Id = Guid.NewGuid(), UserId = userId,
                QuizQuestionId = question.Id, SelectedOptionId = optionId, IsCorrect = option.IsCorrect,
                ScoreAwarded = points, AnsweredAtUtc = now });
            var progress = await db.TeaMapUserPlaceLearningProgress.SingleOrDefaultAsync(
                p => p.UserId == userId && p.PlaceId == question.PlaceId, ct);
            if (progress == null)
            {
                progress = new() { Id = Guid.NewGuid(), UserId = userId, PlaceId = question.PlaceId };
                db.TeaMapUserPlaceLearningProgress.Add(progress);
            }
            progress.Attempts++;
            if (option.IsCorrect) progress.CorrectAnswers++;
            progress.Status = option.IsCorrect && progress.CorrectAnswers >= 3 ?
                TehranLearningStatuses.Mastered : TehranLearningStatuses.Learning;
            progress.LastAnsweredAtUtc = now;
            progress.NextReviewAtUtc = now.AddDays(option.IsCorrect ? Math.Min(14, progress.CorrectAnswers * 3) : 1);
            quiz.Score += points;
            db.TeaMapScoreTransactions.Add(new() { Id = Guid.NewGuid(), UserId = userId,
                Amount = points, ReferenceId = question.Id, CreatedAtUtc = now });
            var profile = await db.TeaMapTehranGameProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
            if (profile == null)
            {
                profile = new() { Id = Guid.NewGuid(), UserId = userId };
                db.TeaMapTehranGameProfiles.Add(profile);
            }
            profile.TotalScore += points;
            // Count the pending answer explicitly; it has not reached SQL Server yet.
            var count = 1 + await db.TeaMapQuizAnswers.CountAsync(a => a.UserId == userId &&
                db.TeaMapQuizQuestions.Any(q => q.Id == a.QuizQuestionId && q.QuizId == quiz.Id), ct);
            var total = await db.TeaMapQuizQuestions.CountAsync(q => q.QuizId == quiz.Id, ct);
            var completed = count == total;
            if (completed)
            {
                var night = await db.TeaMapLearningNights.SingleAsync(n => n.Id == quiz.NightId, ct);
                night.IsCompleted = true;
                quiz.Status = TehranQuizStatuses.Completed;
                quiz.CompletedAtUtc = now;
                var today = DateOnly.FromDateTime(now.AddHours(3.5));
                var last = profile.LastPlayedAtUtc is { } played ? DateOnly.FromDateTime(played.AddHours(3.5)) : (DateOnly?)null;
                profile.CurrentStreak = last == today ? profile.CurrentStreak : last == today.AddDays(-1) ? profile.CurrentStreak + 1 : 1;
                profile.LongestStreak = Math.Max(profile.LongestStreak, profile.CurrentStreak);
                profile.LastPlayedAtUtc = now;
            }
            await db.SaveChangesAsync(ct);
            return new TehranAnswerResult(true, option.IsCorrect, points, completed, quiz.Score, correctName,
                completed ? null : await GetCurrentQuestionAsync(userId, quiz.Id, ct)) { MaximumScore = total * 10 };
        }, ct);

    private async Task<T> InTransaction<T>(Guid userId, Func<Task<T>> operation, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Serialize mutations per user across processes and simultaneous clicks.
            if (db.Database.IsSqlServer())
                await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
                    .SingleAsync(ct);
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
    }
}
