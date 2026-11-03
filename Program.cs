using PriceMonitor.Worker;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// Читаем токен из appsettings.json (который мы скрыли в .gitignore)
var botToken = builder.Configuration["Telegram:BotToken"];

if (string.IsNullOrEmpty(botToken))
{
    throw new Exception("Токен бота не найден в appsettings.json!");
}

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

var host = builder.Build();
host.Run();