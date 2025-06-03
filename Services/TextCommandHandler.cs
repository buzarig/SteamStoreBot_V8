using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamStoreBot_V8.Interfaces; // тут лежить ApiClient (namespace має бути тим самим)
using SteamStoreBot_V8.Utils;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot_V8.Services
{
    internal class TextCommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ApiClient _apiClient;
        private readonly IUserService _userService;
        private readonly StateHandler _stateHandler;

        public TextCommandHandler(
            ITelegramBotClient botClient,
            ApiClient apiClient,
            IUserService userService,
            StateHandler stateHandler
        )
        {
            _botClient = botClient;
            _apiClient = apiClient;
            _userService = userService;
            _stateHandler = stateHandler;
        }

        public async Task HandleCommandTextAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            switch (message)
            {
                case "/start":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "👋 Привіт! Я допоможу знайти ігри в Steam.\nОбери, будь ласка, одну з кнопок нижче:",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "📜 Список бажань":
                    await SendWishlistAsync(chatId, cancellationToken);
                    break;

                case "❌ Видалити з вішліста":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "🗑️ Щоб видалити гру, введіть її <b>ID</b> нижче або натисніть «⬅️ Назад» для відміни:",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: new ReplyKeyboardMarkup(
                            new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                        )
                        {
                            ResizeKeyboard = true,
                        },
                        cancellationToken: cancellationToken
                    );
                    _stateHandler.SetState(chatId, "WaitingForRemoveId");
                    break;

                case "⬅️ Назад":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "🔙 Повернулися до головного меню. Що далі?",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "📰 Підписка на новини":
                    await SendSubscribedGamesListAsync(chatId, cancellationToken);
                    break;

                case "❌ Відписатися від новин":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "📰 Щоб відписатися, введіть <b>ID гри</b> нижче або натисніть «⬅️ Назад» для відміни:",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: new ReplyKeyboardMarkup(
                            new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                        )
                        {
                            ResizeKeyboard = true,
                        },
                        cancellationToken: cancellationToken
                    );
                    _stateHandler.SetState(chatId, "WaitingForUnsubscribeId");
                    break;

                case "🔥 Щоденні знижки":
                    await SendDailyDiscountsAsync(chatId, cancellationToken);
                    break;

                case "🔎 Пошук ігор":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "🔍 Оберіть тип пошуку:",
                        replyMarkup: KeyboardManager.GetSearchKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "🖊️ Пошук по назві":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "🔍 Введіть, будь ласка, назву гри:",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _stateHandler.SetState(chatId, "WaitingForName");
                    break;

                case "📚 Пошук по жанру":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "📚 Введіть жанр англійською (наприклад: Action, RPG, Indie тощо). Якщо сумніваєтесь, просто скопіюйте зі списку:\n"
                            + string.Join(", ", StateHandler.RecognizedGenres),
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _stateHandler.SetState(chatId, "WaitingForGenre");
                    break;

                case "💰 Пошук по бюджету":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "💰 Введіть бюджет у доларах від 0$ до 100$:",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _stateHandler.SetState(chatId, "WaitingForBudget");
                    break;

                case "/help":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "🆘 Ось як я можу допомогти:\n"
                            + "/start — почати спілкування\n"
                            + "📜 Список бажань — переглянути або керувати\n"
                            + "🔎 Пошук ігор — знайти гру за назвою, жанром або бюджетом\n"
                            + "📰 Підписка на новини — отримувати повідомлення про нові релізи й знижки\n"
                            + "🔥 Щоденні знижки — подивитися найцікавіші акції сьогодні",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                default:
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❓ Вибач, я не розумію цю команду. Спробуй одну з кнопок нижче:",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;
            }
        }

        #region Private methods

        private async Task SendWishlistAsync(long chatId, CancellationToken cancellationToken)
        {
            var settings = await _userService.GetSettingsAsync(chatId);
            if (!settings.Wishlist.Any())
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Список бажань порожній.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            var tasks = settings.Wishlist.Select(appId => _apiClient.GetGameDetailsAsync(appId));
            var detailsArray = await Task.WhenAll(tasks);

            var sb = new StringBuilder("Ваш список бажань:\n");
            foreach (var details in detailsArray)
            {
                if (details.TryGetValue("data", out var raw) && raw is JsonElement json)
                {
                    var name = json.GetProperty("name").GetString();
                    var id = json.GetProperty("steam_appid").GetInt32();
                    sb.AppendLine($"🎮 {name} (ID: {id})");
                }
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: sb.ToString(),
                replyMarkup: KeyboardManager.GetWishlistKeyboard(),
                cancellationToken: cancellationToken
            );
        }

        private async Task SendSubscribedGamesListAsync(
            long chatId,
            CancellationToken cancellationToken
        )
        {
            var user = await _userService.GetSettingsAsync(chatId);

            if (!user.SubscribedGames.Any())
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗ Ви ще не підписані на жодні новини.",
                    replyMarkup: KeyboardManager.GetMainKeyboard(),
                    cancellationToken: cancellationToken
                );
                return;
            }

            var sb = new StringBuilder("📬 Підписані на новини ігор:\n\n");
            foreach (var appId in user.SubscribedGames)
            {
                var details = await _apiClient.GetGameDetailsAsync(appId);
                if (details.TryGetValue("data", out var raw) && raw is JsonElement json)
                {
                    var name = json.GetProperty("name").GetString();
                    sb.AppendLine($"▪️ {name} (ID: {appId})");
                }
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: sb.ToString(),
                replyMarkup: KeyboardManager.GetSubscriptionKeyboard(),
                cancellationToken: cancellationToken
            );
        }

        private async Task SendDailyDiscountsAsync(long chatId, CancellationToken cancellationToken)
        {
            var games = await _apiClient.GetDiscountedGamesAsync();
            if (!games.Any())
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "😕 Жодних знижок сьогодні немає.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            var lines = games.Select(g => $"▪️ {g.Name} (ID: {g.Id}) – {g.Discount}%").ToList();
            var text = "🔥 <b>ТОП знижок сьогодні:</b>\n\n" + string.Join("\n", lines);

            var user = await _userService.GetSettingsAsync(chatId);
            var inlineMarkup = new InlineKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            user.SubscriptionOnSales
                                ? "🔕 Відписатися від знижок"
                                : "🔔 Підписатися на знижки",
                            user.SubscriptionOnSales ? "unsubscribe_sales" : "subscribe_sales"
                        ),
                    },
                }
            );

            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: inlineMarkup,
                cancellationToken: cancellationToken
            );

            await _botClient.SendMessage(
                chatId: chatId,
                text: "⬅️ Повернутися у меню",
                replyMarkup: new ReplyKeyboardMarkup(
                    new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                )
                {
                    ResizeKeyboard = true,
                },
                cancellationToken: cancellationToken
            );
        }

        #endregion
    }
}
