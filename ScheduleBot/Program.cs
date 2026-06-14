using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
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

var botToken = builder.Configuration["Telegram:BotToken"] 
    ?? throw new InvalidOperationException("Bot token not found");

var proxyUrl = builder.Configuration["Proxy:Url"];
HttpClientHandler httpClientHandler = new();

if (!string.IsNullOrEmpty(proxyUrl))
{
    var proxy = new System.Net.WebProxy(proxyUrl);
    httpClientHandler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
}

var httpClient = new HttpClient(httpClientHandler);

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken, httpClient));
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<MessageHandler>();
builder.Services.AddScoped<CycleTrackerService>();
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