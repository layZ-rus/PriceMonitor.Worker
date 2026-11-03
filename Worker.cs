using HtmlAgilityPack;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PriceMonitor.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;
    private readonly ITelegramBotClient _botClient;

    // Курс фунта к рублю для перевода цен (примерный, для теста)
    private const decimal GbpToRubRate = 115.50m;

    public Worker(ILogger<Worker> logger, ITelegramBotClient botClient)
    {
        _logger = logger;
        _botClient = botClient;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Служба запущена.");

        // Запускаем слушатель Telegram-бота в фоне
        _ = Task.Run(() => StartBotListenerAsync(stoppingToken), stoppingToken);

        // Основной цикл парсера
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Начинаю проверку цен...");

            using (var db = new AppDbContext())
            {
                var itemsToCheck = await db.TrackedItems.ToListAsync(stoppingToken);

                foreach (var item in itemsToCheck)
                {
                    var newPriceGbp = await ParsePriceAsync(item.Url);

                    if (newPriceGbp.HasValue)
                    {
                        // Переводим в рубли
                        decimal newPriceRub = newPriceGbp.Value * GbpToRubRate;

                        // Если цена упала (сравниваем в рублях)
                        if (newPriceRub < item.LastPrice)
                        {
                            _logger.LogInformation($"Цена упала! {item.ProductName}: {item.LastPrice} руб. -> {newPriceRub:F2} руб.");

                            try
                            {
                                await _botClient.SendMessage(
                                    chatId: item.ChatId,
                                    text: $"📉 <b>Цена упала!</b>\n\nТовар: {item.ProductName}\nБыло: {item.LastPrice:F2} руб.\nСтало: {newPriceRub:F2} руб.\n\n<a href='{item.Url}'>Ссылка на товар</a>",
                                    parseMode: ParseMode.Html,
                                    cancellationToken: stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Ошибка отправки: {ex.Message}");
                            }
                        }

                        // Обновляем цену в базе
                        item.LastPrice = newPriceRub;
                        item.LastChecked = DateTime.UtcNow;
                    }
                }
                await db.SaveChangesAsync(stoppingToken);
            }

            _logger.LogInformation("Жду 30 секунд...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task StartBotListenerAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        await _botClient.ReceiveAsync(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } messageText } message) return;

        var chatId = message.Chat.Id;
        var text = messageText.Trim().ToLower();

        if (text == "/start")
        {
            await botClient.SendMessage(chatId,
                "👋 Привет! Я бот-мониторинг цен.\n\n" +
                "Доступные команды:\n" +
                "/list - Посмотреть список отслеживаемых товаров\n" +
                "/add [ссылка] - Добавить товар (пока работает только для books.toscrape.com)",
                cancellationToken: cancellationToken);
        }
        else if (text == "/list")
        {
            using var db = new AppDbContext();
            var items = await db.TrackedItems.Where(x => x.ChatId == chatId).ToListAsync(cancellationToken);

            if (!items.Any())
            {
                await botClient.SendMessage(chatId, "У вас пока нет отслеживаемых товаров. Добавьте первый через /add", cancellationToken: cancellationToken);
                return;
            }

            string response = "📋 <b>Ваши товары:</b>\n\n";
            foreach (var item in items)
            {
                response += $"🔹 <b>{item.ProductName}</b>\nЦена: {item.LastPrice:F2} руб.\n<a href='{item.Url}'>Ссылка</a>\n\n";
            }
            await botClient.SendMessage(chatId, response, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
        else if (text.StartsWith("/add "))
        {
            string url = messageText.Substring(5).Trim();

            if (!url.StartsWith("http"))
            {
                await botClient.SendMessage(chatId, "❌ Пожалуйста, отправьте корректную ссылку (начиная с http).", cancellationToken: cancellationToken);
                return;
            }

            await botClient.SendMessage(chatId, "⏳ Парсю страницу, подождите...", cancellationToken: cancellationToken);

            var priceGbp = await ParsePriceAsync(url);
            if (priceGbp == null)
            {
                await botClient.SendMessage(chatId, "❌ Не удалось спарсить цену по этой ссылке. Проверьте правильность URL или структуру сайта.", cancellationToken: cancellationToken);
                return;
            }

            decimal priceRub = priceGbp.Value * GbpToRubRate;

            // Пытаемся вытащить название товара
            string productName = await ParseTitleAsync(url) ?? "Неизвестный товар";

            using var db = new AppDbContext();
            var existingItem = await db.TrackedItems.FirstOrDefaultAsync(x => x.Url == url && x.ChatId == chatId, cancellationToken);

            if (existingItem != null)
            {
                await botClient.SendMessage(chatId, "⚠️ Вы уже отслеживаете этот товар.", cancellationToken: cancellationToken);
                return;
            }

            db.TrackedItems.Add(new TrackedItem
            {
                ChatId = chatId,
                Url = url,
                ProductName = productName,
                LastPrice = priceRub,
                LastChecked = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);

            await botClient.SendMessage(chatId,
                $"✅ Товар добавлен!\n\n<b>Название:</b> {productName}\n<b>Текущая цена:</b> {priceRub:F2} руб.\n\nБот будет следить за ценой и сообщит, если она упадет.",
                parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError($"Ошибка Telegram API: {exception.Message}");
        return Task.CompletedTask;
    }

    private async Task<string?> ParseTitleAsync(string url)
    {
        try
        {
            string html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
            return titleNode?.InnerText.Trim();
        }
        catch { return null; }
    }

    private async Task<decimal?> ParsePriceAsync(string url)
    {
        try
        {
            string html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var priceNode = doc.DocumentNode.SelectSingleNode("//p[@class='price_color']");

            if (priceNode != null)
            {
                string cleanPrice = priceNode.InnerText.Replace("£", "").Replace("Â", "").Trim();
                if (decimal.TryParse(cleanPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                {
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка при парсинге {url}: {ex.Message}");
        }
        return null;
    }
}