using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamStoreBot_V8.Interfaces; // тут лежить ApiClient (namespace має бути тим самим)
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SteamStoreBot_V8.Utils
{
    internal class NotificationService
    {
        private readonly IUserService _userService;
        private readonly ApiClient _apiClient;
        private readonly ITelegramBotClient _bot;

        public NotificationService(
            IUserService userService,
            ApiClient apiClient,
            ITelegramBotClient bot
        )
        {
            _userService = userService;
            _apiClient = apiClient;
            _bot = bot;
        }

        public async Task RunSchedulerAsync()
        {
            DateTime lastDiscountRun = DateTime.Now.AddMinutes(-30);
            DateTime lastNewsRun = DateTime.Now.AddHours(-1);

            while (true)
            {
                try
                {
                    var now = DateTime.Now;
                    if (now - lastDiscountRun >= TimeSpan.FromMinutes(30))
                    {
                        Console.WriteLine("📤 Запуск розсилки знижок...");
                        await SendTopDiscountsAsync();
                        lastDiscountRun = DateTime.Now;
                    }

                    if (now - lastNewsRun >= TimeSpan.FromHours(1))
                    {
                        Console.WriteLine("📰 Запуск розсилки новин...");
                        await SendDlcNewsAsync();
                        lastNewsRun = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❗ Помилка у RunSchedulerAsync: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }

        public async Task SendTopDiscountsAsync()
        {
            Console.WriteLine("👉 Починаю розсилку знижок...");
            var users = await _userService.GetAllUsersAsync();
            var games = await _apiClient.GetDiscountedGamesAsync();
            if (games == null || games.Count == 0)
                return;

            var sb = new StringBuilder("🔥 <b>ТОП 10 знижок сьогодні:</b>\n\n");

            Console.WriteLine($"🧾 Ігор зі знижками: {games.Count}");

            var subscribers = users.Where(u => u.SubscriptionOnSales).ToList();

            Console.WriteLine($"👤 Користувачів з підпискою на знижки: {subscribers.Count}");

            foreach (var g in games.Take(10))
            {
                sb.AppendLine(
                    $"🎮 <b>{g.Name}</b>\n💸 Знижка: {g.Discount}%\nhttps://store.steampowered.com/app/{g.Id}\n"
                );
            }

            foreach (var user in users.Where(u => u.SubscriptionOnSales))
            {
                try
                {
                    await _bot.SendMessage(user.ChatId, sb.ToString(), parseMode: ParseMode.Html);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"❗ Не вдалося надіслати повідомлення {user.ChatId}: {ex.Message}"
                    );
                }
            }
        }

        public async Task SendDlcNewsAsync()
        {
            Console.WriteLine("👉 Починаю розсилку новин...");
            var users = await _userService.GetAllUsersAsync();
            foreach (var user in users.Where(u => u.SubscribedGames.Any()))
            {
                foreach (var appId in user.SubscribedGames)
                {
                    var news = await _apiClient.GetGameNewsAsync(appId);
                    if (news == null || news.Count == 0)
                        continue;

                    var item = news[0];
                    var title = item["title"]?.ToString();
                    var url = item["url"]?.ToString();

                    var total = users.Count(u => u.SubscribedGames.Any());
                    Console.WriteLine($"📣 Користувачів, підписаних на ігри: {total}");

                    var message = $"📰 <b>{title}</b>\n{url}";

                    try
                    {
                        await _bot.SendMessage(user.ChatId, message, parseMode: ParseMode.Html);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"❗ Не вдалося надіслати новину користувачу {user.ChatId}: {ex.Message}"
                        );
                    }
                }
            }
        }
    }
}
