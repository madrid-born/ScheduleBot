using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using ScheduleBot.BotHandlers;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;

var root = Path.GetFullPath(args.FirstOrDefault() ?? "ScheduleBot");
var artifacts = Path.Combine(root, "Tests", "artifacts");
Directory.CreateDirectory(artifacts);
var maps = new TehranMapCatalog(Path.Combine(root, "Data", "Tehran", "roads.osm.json.gz"));
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
Check(maps.AdditionalPlaces.Count == 121 && maps.AdditionalPlaces.Select(p => p.Name).Distinct().Count() == 121,
    "121 distinct additional named streets with deterministic OSM identifiers");
Check(!maps.RenderSvg([]).Contains("<polyline"), "new map is completely empty");
Check(maps.DirectionFrom(new Place { Name = "Keshavarz Boulevard" }, new Place { Name = "Enghelab Street" }) == "North-west", "Keshavarz relative to Enghelab is north-west, not north");
File.WriteAllBytes(Path.Combine(artifacts, "night-empty.png"), maps.RenderPng([], showNames: true));
foreach (var place in maps.AdditionalPlaces)
{
    if (!maps.CanRender(place)) throw new Exception("Missing geometry: " + place.Name);
    var svg = maps.RenderSvg([place], place);
    if (XDocument.Parse(svg).Descendants().Where(e => e.Name.LocalName == "text").Any(e => e.Value == place.Name))
        throw new Exception("Quiz leaks street label: " + place.Name);
    var bytes = maps.RenderPng([place], place);
    if (bytes.Length <= 1500 || !bytes.Take(4).SequenceEqual(new byte[] {137, 80, 78, 71}))
        throw new Exception("Invalid PNG: " + place.Name);
}
Console.WriteLine("PASS all 121 imported streets have geometry and render unlabeled quiz PNGs");
if (args.Contains("--offline")) { Console.WriteLine("Offline map checks passed."); return; }
var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "appsettings.json")).AddEnvironmentVariables().Build();
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(configuration.GetConnectionString("DefaultConnection"), s => s.EnableRetryOnFailure()).Options;
var fixtureId = Guid.NewGuid();
var markIndex = Array.IndexOf(args, "--mark-known");
if (markIndex >= 0)
{
    if (markIndex + 1 >= args.Length || !Guid.TryParse(args[markIndex + 1], out var targetUser)) throw new Exception("Explicit user ID required.");
    await using var maintenance = new AppDbContext(options);
    var user = await maintenance.Users.AsNoTracking().SingleAsync(u => u.Id == targetUser);
    var learning = new TehranLearningService(maintenance, maps);
    var added = await learning.MarkPriorKnowledgeAsync(user.Id);
    Console.WriteLine($"Marked {added} requested places known for {user.Name}; learned total {(await learning.GetLearntPlacesAsync(user.Id)).Count}. Scores/history unchanged.");
    return;
}
if (args.Contains("--inspect-users"))
{
    await using var inspect = new AppDbContext(options);
    var users = await inspect.Users.AsNoTracking().Where(u => u.ChatId > 0 && u.IsAccepted)
        .Select(u => new { u.Id, u.Name, Nights = inspect.TeaMapLearningNights.Count(n => n.UserId == u.Id),
            Learnt = inspect.TeaMapUserPlaceLearningProgress.Count(p => p.UserId == u.Id && p.IsLearnt) }).ToListAsync();
    Console.WriteLine(JsonSerializer.Serialize(users));
    var names = await inspect.TeaMapPlaces.AsNoTracking().Where(p => p.Name.Contains("Valiasr") || p.Name.Contains("Beheshti") ||
        p.Name.Contains("Sohrev") || p.Name.Contains("Shariati") || p.Name.Contains("Soleimani") || p.Name.Contains("Hemat") ||
        p.Name.Contains("Hemmat") || p.Name.Contains("Haghani") || p.Name.Contains("Sayyad"))
        .Select(p => new { p.Id, p.Name, p.Source, p.ExternalId }).ToListAsync();
    Console.WriteLine(JsonSerializer.Serialize(names));
    return;
}
if (args.Contains("--sarah-map"))
{
    await using var db = new AppDbContext(options);
    var known = await new TehranLearningService(db, maps).GetLearntPlacesAsync(Guid.Parse("4658382f-31e6-4f55-1fb2-08dee66cf423"));
    var svg = maps.RenderSvg(known, showNames: true);
    var xml = XDocument.Parse(svg);
    var boxes = xml.Descendants().Where(e => e.Attribute("data-label-box") != null)
        .Select(e => e.Attribute("data-label-box")!.Value.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToList();
    Check(boxes.Count == known.Count, "every Sarah place has a positioned label");
    for (var i = 0; i < boxes.Count; i++)
        for (var j = i + 1; j < boxes.Count; j++)
        {
            var a = boxes[i]; var b = boxes[j];
            Check(!(a[0] < b[0] + b[2] && a[0] + a[2] > b[0] && a[1] < b[1] + b[3] && a[1] + a[3] > b[1]), "label boxes do not overlap");
        }
    Check(known.All(p => xml.Descendants().Any(e => e.Name.LocalName == "text" && e.Value == p.Name)), "all learned names remain visible");
    Check(known.Count(maps.IsHighway) == 4, "Sarah's four highways use the highway style");
    Check(svg.Contains("data-road-kind=\"highway\"") && svg.Contains("data-road-kind=\"street\""), "map has separate road styles");
    Check(!maps.RenderSvg(known, known[0]).Contains("data-label-box"), "quiz remains unlabeled");
    File.WriteAllBytes(Path.Combine(artifacts, "sarah-map-readable.png"), maps.RenderPng(known, showNames: true));
    var all = await new TehranLearningService(db, maps).GetLearningPlacesAsync();
    var crowded = maps.RenderSvg(all, showNames: true);
    Check(all.All(p => XDocument.Parse(crowded).Descendants().Any(e => e.Name.LocalName == "tspan" && e.Value == p.Name)), "crowded maps retain all names in a numbered legend");
    Check(maps.RenderPng(all, showNames: true).Length > 1000, "full catalog map renders with expandable legend");
    return;
}
var otherFixtureId = Guid.NewGuid();
const long fixtureChatId = -910000001;
var transport = new TelegramTransport();
using var http = new HttpClient(transport);
var bot = new TelegramBotClient("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk", http);
var serviceConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Telegram:AdminChatId"] = "0" }).Build();
var provider = new ServiceCollection().BuildServiceProvider();
var main = new MainService(bot, provider, serviceConfiguration, new TestEnvironment(), new UserSessionService());
var clock = new TestClock();
AppDbContext Context() => new(options);
UpdateData Click(string value) => new() { ChatId = fixtureChatId, DataSeparated = ("Tehran|" + value).Split('|').ToList() };
try
{
    await using (var db = Context())
    {
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "SQL Server schema has nightly migration");
        await new TehranCatalogSeeder(db, maps).EnsureAsync();
        var count = await db.TeaMapPlaces.CountAsync();
        await new TehranCatalogSeeder(db, maps).EnsureAsync();
        Check(count >= 120 && await db.TeaMapPlaces.CountAsync() == count, "expanded SQL catalog is idempotent");
        Console.WriteLine("SQL catalog total: " + count);
        db.Users.Add(new() { Id = fixtureId, ChatId = fixtureChatId, Name = "Nightly flow test " + fixtureId, IsAccepted = false });
        db.Users.Add(new() { Id = otherFixtureId, ChatId = fixtureChatId - 1, Name = "Ownership test " + otherFixtureId, IsAccepted = false });
        await db.SaveChangesAsync();
    }
    await using var flowDb = Context();
    var learning = new TehranLearningService(flowDb, maps, clock);
    var handler = new TehranLearningHandler(new DatabaseService(flowDb, main), learning, maps, main, bot);
    Check((await learning.GetLearningPlacesAsync()).Count >= 100, "at least 100 learnable places in SQL Server");
    Check((await learning.GetLearntPlacesAsync(fixtureId)).Count == 0, "first night starts at zero learned");
    await handler.HandleSection(new() { ChatId = fixtureChatId });
    Check(transport.Requests.Last().Body.Contains("LEARN") && transport.Requests.Last().Body.Contains("START") && transport.Requests.Last().Body.Contains("MAP"), "entry has Learn, Quiz and Map");
    await handler.HandleCallBack(Click("MAP"));
    Check(transport.Requests.Last().Method == "sendPhoto" && transport.Requests.Last().Body.Contains("0 places learned"), "Map button sends empty map initially");
    await handler.HandleCallBack(Click("START"));
    Check(await flowDb.TeaMapQuizzes.CountAsync(q => q.UserId == fixtureId) == 0, "quiz locked before two lessons");
    var night = await learning.GetNightAsync(fixtureId) ?? throw new Exception("Missing night");
    Check(!await learning.ConfirmLessonAsync(fixtureId, night.Id, 1), "cannot skip the first lesson");
    Check(!await learning.ConfirmLessonAsync(otherFixtureId, night.Id, 0), "another user cannot confirm your lesson");
    await handler.HandleCallBack(Click("LEARN"));
    Check(transport.Requests.Last().Body.Contains("lesson 1/2") && (await learning.GetLearntPlacesAsync(fixtureId)).Count == 0,
        "previewing lesson does not mark it learned");
    async Task<bool> Confirm() { await using var db = Context(); return await new TehranLearningService(db, maps, clock).ConfirmLessonAsync(fixtureId, night.Id, 0); }
    Check((await Task.WhenAll(Confirm(), Confirm())).Count(x => x) == 1, "concurrent lesson confirmations unlock only one place");
    await handler.HandleCallBack(Click($"DONE|{night.Id:N}|0"));
    Check(transport.Requests.Last().Body.Contains("already confirmed"), "duplicate lesson callback is safe");
    await handler.HandleCallBack(Click("LEARN"));
    Check(transport.Requests.Last().Body.Contains("lesson 2/2"), "second lesson resumes after restart");
    Check((await learning.GetLearntPlacesAsync(fixtureId)).Count == 1 && await learning.StartQuizAsync(fixtureId) == null,
        "one learned place is insufficient for nightly quiz");
    await handler.HandleCallBack(Click($"DONE|{night.Id:N}|1"));
    Check(transport.Requests.Last().Method == "sendPhoto" && transport.Requests.Last().Body.Contains("Question 1/5"), "first two places unlock five genuinely different questions");
    var known = await learning.GetLearntPlacesAsync(fixtureId);
    Check(known.Count == 2 && (await learning.GetLearntPlacesAsync(otherFixtureId)).Count == 0, "IsLearnt is per-user and exactly two places unlock");
    var unknown = (await learning.GetLearningPlacesAsync()).First(p => known.All(k => k.Id != p.Id));
    var quiz = await flowDb.TeaMapQuizzes.AsNoTracking().SingleAsync(q => q.UserId == fixtureId && q.Status == "in_progress");
    var legacy = await flowDb.TeaMapQuizQuestions.FirstAsync(q => q.QuizId == quiz.Id && q.QuestionType == TehranLearningService.OrientationQuestionType);
    var legacyOption = await flowDb.TeaMapQuizQuestionOptions.AsNoTracking().FirstAsync(o => o.QuizQuestionId == legacy.Id);
    legacy.Prompt = "Which direction does the red street mostly run?";
    await flowDb.SaveChangesAsync();
    Check(await learning.SubmitAnswerAsync(fixtureId, legacyOption.Id) == null, "old unanswered buttons trigger quiz repair without awarding incorrect points");
    Check(!await flowDb.TeaMapQuizQuestions.AnyAsync(q => q.Id == legacy.Id), "repair replaces only pending legacy questions");
    var questions = await flowDb.TeaMapQuizQuestions.AsNoTracking().Where(q => q.QuizId == quiz.Id).ToListAsync();
    var questionCount = questions.Count;
    Check(questionCount == 5 && questions.Select(q => q.QuestionType).Distinct().Count() == 3, "early round mixes three types without padding to 15");
    Check(questions.Select(q => TehranLearningService.QuestionKey(q.QuestionType, q.PlaceId, q.ReferencePlaceId)).Distinct().Count() == questionCount, "no repeated question or reverse relationship in a night");
    var directionQuestion = questions.Single(q => q.QuestionType == TehranLearningService.DirectionQuestionType);
    var directionChoices = await flowDb.TeaMapQuizQuestionOptions.Where(o => o.QuizQuestionId == directionQuestion.Id).Select(o => o.Text).ToListAsync();
    Check(directionChoices.Order().SequenceEqual(new[] { "North-west", "North-east", "South-west", "South-east" }.Order()), "diagonal question offers exactly the four diagonal answers");
    Check(questions.All(q => known.Any(p => p.Id == q.PlaceId) && (q.ReferencePlaceId == null || known.Any(p => p.Id == q.ReferencePlaceId))), "every question uses learned places only");
    var named = maps.RenderSvg(known, showNames: true);
    Check(known.All(p => XDocument.Parse(named).Descendants().Any(e => e.Name.LocalName == "text" && e.Value == p.Name)) &&
        !named.Contains(unknown.Name), "personal map names exactly the unlocked places");
    File.WriteAllBytes(Path.Combine(artifacts, "night-learned-map.png"), maps.RenderPng(known, showNames: true));
    foreach (var type in questions.Select(q => q.QuestionType).Distinct())
    {
        var q = questions.First(q => q.QuestionType == type);
        var place = known.Single(p => p.Id == q.PlaceId);
        var reference = known.SingleOrDefault(p => p.Id == q.ReferencePlaceId);
        var svg = maps.RenderSvg(known, place, reference);
        Check(!XDocument.Parse(svg).Descendants().Any(e => e.Name.LocalName == "text" && known.Any(p => p.Name == e.Value)), "no place names in " + type + " picture");
        Check(!svg.Contains(unknown.Name), "no unknown place labels in quiz");
        File.WriteAllBytes(Path.Combine(artifacts, "night-quiz-" + type + ".png"), maps.RenderPng(known, place, reference));
    }
    await handler.HandleCallBack(Click("START"));
    Check(await flowDb.TeaMapQuizzes.CountAsync(q => q.UserId == fixtureId) == 1, "Quiz button resumes the same nightly quiz");
    var current = await learning.GetCurrentQuestionAsync(fixtureId, quiz.Id) ?? throw new Exception("No question");
    var first = await flowDb.TeaMapQuizQuestionOptions.AsNoTracking().SingleAsync(o => o.QuizQuestionId == current.QuestionId && o.IsCorrect);
    Check(await learning.GetCurrentQuestionAsync(otherFixtureId, quiz.Id) == null && await learning.SubmitAnswerAsync(otherFixtureId, first.Id) == null, "quiz ownership enforced");
    var future = await flowDb.TeaMapQuizQuestionOptions.AsNoTracking().FirstAsync(o => flowDb.TeaMapQuizQuestions.Any(q => q.Id == o.QuizQuestionId && q.QuizId == quiz.Id && q.Position == questionCount - 1));
    Check(await learning.SubmitAnswerAsync(fixtureId, future.Id) == null, "out-of-order answer rejected");
    async Task<TehranAnswerResult?> Submit() { await using var db = Context(); return await new TehranLearningService(db, maps, clock).SubmitAnswerAsync(fixtureId, first.Id); }
    var concurrent = await Task.WhenAll(Submit(), Submit());
    Check(concurrent.Count(r => r?.Accepted == true) == 1 && concurrent.Single(r => r?.Accepted == true)!.MaximumScore == questionCount * 10, "retry-safe concurrent answers award once with correct maximum score");
    await handler.HandleCallBack(Click("ANSWER|" + first.Id));
    Check(transport.Requests.Last().Body.Contains("already answered"), "duplicate answer feedback");
    var answered = 1;
    while ((current = await learning.GetCurrentQuestionAsync(fixtureId, quiz.Id)) != null)
    {
        var correct = answered != 1;
        var option = await flowDb.TeaMapQuizQuestionOptions.AsNoTracking().FirstAsync(o => o.QuizQuestionId == current.QuestionId && o.IsCorrect == correct);
        var click = Click("ANSWER|" + option.Id); click.MessageId = 1;
        transport.RejectNextEdit = answered == 1;
        await handler.HandleCallBack(click);
        answered++;
        var nextQuestion = await learning.GetCurrentQuestionAsync(fixtureId, quiz.Id);
        Check(transport.Requests.Last().Method == (nextQuestion?.QuestionType == TehranLearningService.MapQuestionType ? "sendPhoto" : "sendMessage"), "recognition uses a photo; compass questions and completion are text-only");
    }
    await using (var verify = Context())
    {
        Check(answered == questionCount && (await verify.TeaMapQuizzes.SingleAsync(q => q.Id == quiz.Id)).Score == 40 &&
            (await verify.TeaMapLearningNights.SingleAsync(n => n.Id == night.Id)).IsCompleted, "shorter first night completes with correct score");
        Check(await verify.TeaMapQuizAnswers.CountAsync(a => a.UserId == fixtureId) == questionCount &&
            await verify.TeaMapScoreTransactions.Where(t => t.UserId == fixtureId).SumAsync(t => t.Amount) == 40 &&
            await verify.TeaMapUserPlaceLearningProgress.Where(p => p.UserId == fixtureId).SumAsync(p => p.Attempts) == questionCount, "answers, ledger and progress agree");
    }
    Check(transport.Requests.Last().Body.Contains("40/50"), "completion feedback reports score");
    Check(await learning.StartQuizAsync(fixtureId) == null && (await learning.GetNightAsync(fixtureId))!.Id == night.Id, "same night cannot unlock extra places or another quiz");
    clock.AdvanceDay();
    // Simulate eight places learned before the bot, as requested for the real learner.
    Check(await learning.MarkPriorKnowledgeAsync(fixtureId) == 8 && await learning.MarkPriorKnowledgeAsync(fixtureId) == 8 &&
        (await learning.GetLearntPlacesAsync(fixtureId)).Count == 10 && (await learning.GetLearntPlacesAsync(otherFixtureId)).Count == 0,
        "all eight requested streets unlock idempotently for only the selected user");
    var next = await learning.GetNightAsync(fixtureId) ?? throw new Exception("Missing next night");
    Check(next.Id != night.Id && next.FirstPlaceId != night.FirstPlaceId && next.SecondPlaceId != night.SecondPlaceId && next.LearntCount == 0,
        "next Tehran date assigns two new unlearned places");
    await learning.ConfirmLessonAsync(fixtureId, next.Id, 0);
    await learning.ConfirmLessonAsync(fixtureId, next.Id, 1);
    Check((await learning.GetLearntPlacesAsync(fixtureId)).Count == 12, "prior knowledge is retained; next night adds only two unseen places");
    transport.RejectNextPhoto = true;
    try { await handler.HandleCallBack(Click("START")); throw new Exception("Expected photo failure"); }
    catch (Telegram.Bot.Exceptions.ApiRequestException) { }
    await handler.HandleCallBack(Click("START"));
    Check(transport.Requests.Last().Method == "sendPhoto" && await flowDb.TeaMapQuizzes.CountAsync(q => q.UserId == fixtureId && q.Status == "in_progress") == 1, "failed photo delivery safely resumes quiz");
    var secondQuiz = await flowDb.TeaMapQuizzes.AsNoTracking().SingleAsync(q => q.UserId == fixtureId && q.NightId == next.Id);
    var fullQuestions = await flowDb.TeaMapQuizQuestions.Where(q => q.QuizId == secondQuiz.Id).ToListAsync();
    Check(fullQuestions.Count == 15 && fullQuestions.Select(q => TehranLearningService.QuestionKey(q.QuestionType, q.PlaceId, q.ReferencePlaceId)).Distinct().Count() == 15, "experienced learner receives exactly 15 unique questions");
    while ((current = await learning.GetCurrentQuestionAsync(fixtureId, secondQuiz.Id)) != null)
    {
        var option = await flowDb.TeaMapQuizQuestionOptions.AsNoTracking().SingleAsync(o => o.QuizQuestionId == current.QuestionId && o.IsCorrect);
        await handler.HandleCallBack(Click("ANSWER|" + option.Id));
    }
    await using (var verify = Context())
    {
        var profile = await verify.TeaMapTehranGameProfiles.SingleAsync(p => p.UserId == fixtureId);
        Check(profile.TotalScore == 190 && profile.CurrentStreak == 2 && profile.LongestStreak == 2,
            "second full 15-question night updates score and consecutive-day streak");
        Check(await verify.TeaMapQuizAnswers.CountAsync(a => a.UserId == fixtureId) == 20, "both nights persist exactly 20 distinct answers");
    }
    clock.AdvanceDay();
    var interrupted = await learning.GetNightAsync(fixtureId);
    clock.AdvanceDay();
    Check(interrupted != null && (await learning.GetNightAsync(fixtureId))!.Id == interrupted.Id &&
        (await learning.GetLearntPlacesAsync(fixtureId)).Count == 12, "unfinished night resumes across date rollover without extra unlocks");
    Console.WriteLine("All nightly flow checks passed against SQL Server. Telegram HTTP simulated; no real messages sent.");
}
finally
{
    await using var db = Context();
    await db.TeaMapQuizAnswers.Where(a => a.UserId == fixtureId).ExecuteDeleteAsync();
    await db.TeaMapQuizzes.Where(q => q.UserId == fixtureId).ExecuteDeleteAsync();
    await db.TeaMapLearningNights.Where(n => n.UserId == fixtureId || n.UserId == otherFixtureId).ExecuteDeleteAsync();
    await db.TeaMapUserPlaceLearningProgress.Where(p => p.UserId == fixtureId).ExecuteDeleteAsync();
    await db.TeaMapScoreTransactions.Where(t => t.UserId == fixtureId).ExecuteDeleteAsync();
    await db.TeaMapTehranGameProfiles.Where(p => p.UserId == fixtureId).ExecuteDeleteAsync();
    await db.Users.Where(u => u.Id == fixtureId || u.Id == otherFixtureId).ExecuteDeleteAsync();
    Console.WriteLine("Synthetic test users and their results removed; expanded catalog retained.");
}

sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 1, 17, 30, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void AdvanceDay() => now = now.AddDays(1);
}
sealed class TelegramTransport : HttpMessageHandler
{
    public List<(string Method, string Body)> Requests { get; } = [];
    public bool RejectNextEdit { get; set; }
    public bool RejectNextPhoto { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.RequestUri!.Segments.Last();
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        foreach (Match match in Regex.Matches(body, "\"callback_data\"\\s*:\\s*\"([^\"]+)\""))
            if (Encoding.UTF8.GetByteCount(match.Groups[1].Value) > 64) throw new Exception("Telegram callback exceeds 64 bytes");
        Requests.Add((method, body));
        if ((method == "editMessageReplyMarkup" && RejectNextEdit) || (method == "sendPhoto" && RejectNextPhoto))
        {
            RejectNextEdit = RejectNextPhoto = false;
            return new(HttpStatusCode.BadRequest) { Content = new StringContent("{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: simulated failure\"}", Encoding.UTF8, "application/json") };
        }
        return new(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true,\"result\":{\"message_id\":1,\"date\":1790900000,\"chat\":{\"id\":-910000001,\"type\":\"private\"}}}", Encoding.UTF8, "application/json") };
    }
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "TehranFlowTests";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
