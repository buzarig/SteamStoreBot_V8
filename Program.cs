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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("botConfig.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var botToken = configuration["TelegramBot:Token"];
            var apiBaseUrl = configuration["Api:BaseUrl"];
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                throw new InvalidOperationException(
                    "Не знайдено адресу вашої API у конфігурації. Перевірте, що в botConfig.json або в змінних оточення є ключ \"Api:BaseUrl\"."
                );
            }

            if (string.IsNullOrWhiteSpace(botToken))
            {
                throw new InvalidOperationException(
                    "Не знайдено TelegramBot:Token — задайте його в botConfig.json або через ENV VARIABLE TELEGRAMBOT__TOKEN"
                );
            }

            var baseUri = new Uri(apiBaseUrl);

            // 2) Налаштовуємо Host та DI Container
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.AddSingleton<IConfiguration>(configuration);

                        services.AddHttpClient<ApiClient>(client =>
                        {
                            client.BaseAddress = baseUri;
                            client.Timeout = TimeSpan.FromSeconds(30);
                        });

                        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(
                            botToken
                        ));

                        services.AddSingleton<IUserService, UserService>();

                        services.AddSingleton<CallbackHandler>();
                        services.AddSingleton<StateHandler>();
                        services.AddSingleton<TextCommandHandler>();
                        services.AddSingleton<NotificationService>();

                        services.AddSingleton<ICommandHandler, CommandHandler>();
                    }
                )
                .UseConsoleLifetime()
                .Build();

            // 3) Отримуємо з DI необхідні екземпляри
            var servicesProvider = host.Services;

            // Ми реєстрували саме ICommandHandler, тому дістанемо його через інтерфейс:
            var commandHandler = servicesProvider.GetRequiredService<ICommandHandler>();
            // Якщо ж у вас строка StartReceiving все ще викликає CommandHandler.HandleCommandAsync,
            // змініть її на HandleAsync (або відповідно адаптуйте).
            var botClient = servicesProvider.GetRequiredService<ITelegramBotClient>();
            var notificationService = servicesProvider.GetRequiredService<NotificationService>();

            // 4) Запускаємо Telegram-Polling
            var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                // Вказуємо, які саме оновлення ми хочемо обробляти
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
                DropPendingUpdates = true,
            };

            botClient.StartReceiving(
                // Перший аргумент — делегат, який бере update → передаємо у наш фасад ICommandHandler
                (bot, update, token) => commandHandler.HandleAsync(update, token),
                // Другий аргумент — обробник помилок
                (bot, ex, token) =>
                {
                    Console.WriteLine($"[Telegram Error] {ex.Message}");
                    return Task.CompletedTask;
                },
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущено. Натисніть Enter для зупинки...");

            // 5) Запускаємо NotificationService у фоні (якщо він у вас запускається віч-loop’ом)
            _ = Task.Run(() => notificationService.RunSchedulerAsync(), cts.Token);

            // Чекаємо, доки користувач натисне Enter
            Console.ReadLine();
            cts.Cancel();

            // Невелика затримка, щоб усі цикли встигли «прибратися»
            await Task.Delay(500);
        }
    }
}
