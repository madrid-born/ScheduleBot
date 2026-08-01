using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var telegramString = builder.Environment.IsDevelopment() ? "Development" : "Telegram";
var botTokenString = builder.Configuration[$"{telegramString}:BotToken"];
var botUrlString = builder.Configuration[$"{telegramString}:Url"];
var adminIdString = builder.Configuration[$"{telegramString}:AdminChatId"];

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
builder.Services.AddSingleton(new MainService(bot) { Url = botUrlString!, BotToken = botTokenString, AdminChatId = long.Parse(adminIdString!) });
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<MessageHandler>();
builder.Services.AddScoped<UserHandler>();
builder.Services.AddScoped<CycleTrackerHandler>();
builder.Services.AddScoped<CycleTrackerService>();
builder.Services.AddScoped<CartHandler>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<TransactionHandler>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddHostedService<BotPollingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();