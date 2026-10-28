using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScheduleBot.BotHandlers;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Environment.IsDevelopment() ? "DefaultConnectionTest" : "DefaultConnection";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(connString),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        }
    );

    if (!builder.Environment.IsDevelopment()) return;
    options.EnableSensitiveDataLogging();
    options.EnableDetailedErrors();
});

var applicationConnection = builder.Configuration.GetConnectionString(connString)
    ?? throw new InvalidOperationException($"Connection string '{connString}' was not found.");
var configuredMetroConnection = builder.Configuration.GetConnectionString("MetroConnection");
var metroConnection = string.IsNullOrWhiteSpace(configuredMetroConnection)
    ? new SqlConnectionStringBuilder(applicationConnection) { InitialCatalog = "metro" }.ConnectionString
    : configuredMetroConnection;

builder.Services.AddDbContext<MetroDbContext>(options =>
{
    options.UseSqlServer(
        metroConnection,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        });
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    if (builder.Environment.IsDevelopment()) options.EnableDetailedErrors();
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var telegramString = builder.Environment.IsDevelopment() ? "Development" : "Production";
var botTokenString = builder.Configuration[$"{telegramString}:BotToken"];

HttpClient httpClient;
if (builder.Environment.IsDevelopment())
{
    var proxyUrl = builder.Configuration["Proxy:Url"];
    httpClient = !string.IsNullOrWhiteSpace(proxyUrl)
        ? new HttpClient(new HttpClientHandler { Proxy = new WebProxy(proxyUrl), UseProxy = true })
        : new HttpClient();
}
else httpClient = new HttpClient();

var botToken = botTokenString ?? throw new InvalidOperationException("Bot token not found");

var bot = new TelegramBotClient(botToken, httpClient);
builder.Services.AddSingleton<ITelegramBotClient>(bot);
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddScoped<MainService>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<FileLinkHandler>();
builder.Services.AddScoped<FileLinkService>();
builder.Services.AddOptions<FileLinkOptions>().Bind(builder.Configuration.GetSection("FileLinks"))
    .Validate(o => !o.Enabled || (o.ExpirationMinutes is > 0 and <= 525600 && o.MaxConcurrentDownloads is > 0 and <= 1024
        && o.RequestsPerMinute is > 0 and <= 100000 && o.MaxActiveLinksPerUser is > 0 and <= 10000
        && o.DownloadTimeoutMinutes is > 0 and <= 1440 && o.MaxDownloadBytes is > 0 and <= 4L * 1024 * 1024 * 1024), "Invalid FileLinks limits.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
if (builder.Configuration.GetValue<bool>("FileLinks:Enabled"))
{
    builder.Services.AddSingleton<TelegramFileService>();
    builder.Services.AddSingleton<MtProtoTelegramFileService>();
    builder.Services.AddSingleton<ITelegramFileSource>(sp => builder.Configuration.GetValue<bool>("FileLinks:UseMtProto")
        ? sp.GetRequiredService<MtProtoTelegramFileService>() : sp.GetRequiredService<TelegramFileService>());
    builder.Services.AddSingleton<FileDownloadLimits>();
    builder.Services.AddScoped<FileDownloadService>();
}
builder.Services.AddScoped<MessageHandler>();
builder.Services.AddScoped<UserHandler>();
builder.Services.AddScoped<CycleTrackerHandler>();
builder.Services.AddScoped<CycleTrackerService>();
builder.Services.AddScoped<CartHandler>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<TransactionHandler>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<ScoreCounterHandler>();
builder.Services.AddScoped<ScoreCounterService>();
builder.Services.AddScoped<SpotifyHandler>();
builder.Services.AddScoped<NotificationHandler>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MetroHandler>();
builder.Services.AddScoped<MetroService>();
builder.Services.AddScoped<MapifyHandler>();
builder.Services.AddScoped<MapifyService>();
builder.Services.AddScoped<TehranLearningHandler>();
builder.Services.AddScoped<TehranLearningService>();
builder.Services.AddScoped<TehranCatalogSeeder>();
builder.Services.AddSingleton(new TehranMapCatalog(Path.Combine(builder.Environment.ContentRootPath,
    "Data", "Tehran", "roads.osm.json.gz")));
builder.Services.AddScoped<SurveyHandler>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<SurveyReportService>();
builder.Services.AddHttpClient<SpotifyService>(client =>
{
    var baseUrl = builder.Configuration["SpotifyApi:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});
builder.Services.AddHostedService<BotPollingService>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("FileLinks:Enabled") &&
    !app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileLinkOptions>>().Value.HasPublicBaseUrl)
    app.Logger.LogWarning("File Links cannot create links until FileLinks:PublicBaseUrl is set to an HTTPS address or an HTTP loopback address for local testing. Other bot features remain available.");

if (builder.Configuration.GetValue<bool>("FileLinks:Enabled") && builder.Configuration.GetValue<bool>("FileLinks:UseMtProto") &&
    !app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileLinkOptions>>().Value.HasMtProtoCredentials)
    app.Logger.LogWarning("MTProto downloads need FileLinks:MtProtoApiId, MtProtoApiHash and a writable MtProtoSessionPath. Configure application credentials before creating file links.");

// Idempotent catalog expansion; schema migrations remain an explicit deployment step.
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<TehranCatalogSeeder>().EnsureAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Nginx on this machine terminates TLS. Retain the default loopback-only proxy trust.
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto });
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
