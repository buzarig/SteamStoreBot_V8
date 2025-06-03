using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SteamStoreBot_V8.Interfaces;
using SteamStoreBot_V8.Services;
using SteamStoreBot_V8.Utils;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace SteamStoreBot_V8
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            // 1) Створюємо конфігурацію: читаємо botConfig.json (якщо є), потім ENV
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("botConfig.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // 2) Зчитуємо Token та BaseUrl (ENV має пріоритет)
            var botToken = configuration["TelegramBot:Token"];
            var apiBaseUrl = configuration["Api:BaseUrl"];

            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                throw new InvalidOperationException(
                    "Не знайдено адресу вашої API у конфігурації. "
                        + "Перевірте, що в botConfig.json чи в Environment Variables є ключ \"Api:BaseUrl\"."
                );
            }

            if (string.IsNullOrWhiteSpace(botToken))
            {
                throw new InvalidOperationException(
                    "Не знайдено TelegramBot:Token — задайте його в botConfig.json або як ENV VARIABLE TELEGRAMBOT__TOKEN"
                );
            }

            var baseUri = new Uri(apiBaseUrl);

            // 3) Налаштовуємо Host та DI-контейнер
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.AddSingleton<IConfiguration>(configuration);

                        // Налаштовуємо HttpClient для ApiClient
                        services.AddHttpClient<ApiClient>(client =>
                        {
                            client.BaseAddress = baseUri;
                            client.Timeout = TimeSpan.FromSeconds(30);
                        });

                        // Telegram BotClient
                        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(
                            botToken
                        ));

                        // Ваші сервіси
                        services.AddSingleton<IUserService, UserService>();
                        services.AddSingleton<CallbackHandler>();
                        services.AddSingleton<StateHandler>();
                        services.AddSingleton<TextCommandHandler>();
                        services.AddSingleton<NotificationService>();

                        // Місток-“фасад” для команд
                        services.AddSingleton<ICommandHandler, CommandHandler>();
                    }
                )
                // Використовуємо ConsoleLifetime, щоб реагувати на SIGTERM/SIGINT правильно
                .UseConsoleLifetime()
                .Build();

            // 4) Дістаємо з DI потрібні сервіси
            var servicesProvider = host.Services;
            var botClient = servicesProvider.GetRequiredService<ITelegramBotClient>();
            var commandHandler = servicesProvider.GetRequiredService<ICommandHandler>();
            var notificationService = servicesProvider.GetRequiredService<NotificationService>();

            // 5) Налаштовуємо Polling (обробник оновлень)
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
                DropPendingUpdates = true,
            };

            // Запускаємо асинхронний Polling
            botClient.StartReceiving(
                (bot, update, token) => commandHandler.HandleAsync(update, token),
                (bot, ex, token) =>
                {
                    Console.WriteLine($"[Telegram Error] {ex.Message}");
                    return Task.CompletedTask;
                },
                receiverOptions,
                cancellationToken: host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
            );

            Console.WriteLine("Бот запущено. Чекаємо повідомлень…");

            // 6) Запускаємо додаткову фонову задачу (NotificationService), якщо вона у вас є
            _ = Task.Run(
                () => notificationService.RunSchedulerAsync(),
                host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
            );

            // 7) Чекаємо, поки хост отримає сигнал завершення (CTRL+C, SIGTERM чи через Render)
            await host.RunAsync();
        }
    }
}
