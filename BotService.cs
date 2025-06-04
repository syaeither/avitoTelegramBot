using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using HtmlAgilityPack;
using System.Text;
using System;
using System.Collections.Generic;

namespace avitoTelegramBot
{
    internal class BotService : BackgroundService
    {
        private readonly TelegramBotClient botClient;
        private CancellationTokenSource cts;

        private readonly HashSet<string> stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "здарова", "привет", "объявление", "объявления", "поиск", "товар", "работа", "продажа",
            "куплю", "продаю", "склад", "звола", "hello", "hi", "test", "тест", "начать", "начни", "старт"
        };


        public BotService()
        {
            botClient = new TelegramBotClient("ТОКЕН вставить");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            await botClient.DeleteWebhookAsync();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = null
            };

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cts.Token
            );

            var me = await botClient.GetMeAsync();
            Console.WriteLine($"Бот @{me.Username} запущен как служба.");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            cts.Cancel();
            await base.StopAsync(cancellationToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update.Type != UpdateType.Message || update.Message?.Text == null)
                return;

            var chatId = update.Message.Chat.Id;
            var query = update.Message.Text.Trim();

            if (query.Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendTextMessageAsync(chatId,
                    "Привет! Я бот для поиска объявлений на Авито.\nНапиши, что ищешь, например: <b>ноутбук Asus</b> или <b>iPhone 14</b>.",
                    parseMode: ParseMode.Html,
                    cancellationToken: token);
                return;
            }

            // Проверка запроса
            if (query.Length < 3 || stopWords.Contains(query.ToLower()))
            {
                await bot.SendTextMessageAsync(chatId,
                    "❗ Пожалуйста, напишите более конкретный запрос. Например: <b>телевизор Samsung</b>.",
                    parseMode: ParseMode.Html,
                    cancellationToken: token);
                return;
            }

            var result = ParseAvito(query);

            await bot.SendTextMessageAsync(chatId, result, parseMode: ParseMode.Html, cancellationToken: token);
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken token)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            return Task.CompletedTask;
        }

        private string ParseAvito(string query)
        {
            try
            {
                string search = Uri.EscapeDataString(query);
                string url = $"https://www.avito.ru/rossiya?q={search}";

                var web = new HtmlWeb
                {
                    UserAgent = "Mozilla/5.0",
                    PreRequest = req => { req.Timeout = 10000; return true; }
                };

                var doc = web.Load(url);
                var items = doc.DocumentNode.SelectNodes("//div[@data-marker='item']");

                if (items == null || items.Count == 0)
                    return "❗ Ничего не найдено по вашему запросу.";

                var sb = new StringBuilder();
                int count = 0;

                foreach (var item in items)
                {
                    if (count++ >= 5) break;

                    string title = item.SelectSingleNode(".//h3")?.InnerText?.Trim()
                        ?? item.SelectSingleNode(".//a[@title]")?.GetAttributeValue("title", null)?.Trim()
                        ?? "Без названия";

                    string price = item.SelectSingleNode(".//meta[@itemprop='price']")?.GetAttributeValue("content", "0") ?? "0";
                    string href = item.SelectSingleNode(".//a[@itemprop='url']")?.GetAttributeValue("href", "");

                    if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                        href = "https://www.avito.ru" + href;

                    sb.AppendLine($"📌 <b>{title}</b>\n💰 {price} ₽\n🔗 {href}\n");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"⚠ Произошла ошибка при получении данных: {ex.Message}";
            }
        }
    }
}
