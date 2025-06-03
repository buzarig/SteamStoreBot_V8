using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SteamStoreBot_V8.Interfaces; // тут лежить ApiClient (namespace має бути тим самим)
using SteamStoreBot_V8.Models;
using SteamStoreBot_V8.Utils;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot_V8.Services
{
    internal class StateHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ApiClient _apiClient;
        private readonly IUserService _userService;

        private readonly Dictionary<long, string> _userStates = new Dictionary<long, string>();
        private readonly Dictionary<long, ReplyKeyboardMarkup> _gameKeyboards =
            new Dictionary<long, ReplyKeyboardMarkup>();
        private readonly Dictionary<long, int> _userMessageToDelete = new Dictionary<long, int>();

        public StateHandler(
            ITelegramBotClient botClient,
            ApiClient apiClient,
            IUserService userService
        )
        {
            _botClient = botClient;
            _apiClient = apiClient;
            _userService = userService;
        }

        public bool HasState(long chatId) => _userStates.ContainsKey(chatId);

        public void SetState(long chatId, string state) => _userStates[chatId] = state;

        public void ClearState(long chatId)
        {
            _userStates.Remove(chatId);
            _gameKeyboards.Remove(chatId);
            _userMessageToDelete.Remove(chatId);
        }

        public async Task HandleStateAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            var state = _userStates[chatId];

            switch (state)
            {
                case "WaitingForName":
                    await HandleWaitingForNameAsync(chatId, message, cancellationToken);
                    break;

                case "WaitingForGenre":
                    await HandleWaitingForGenreAsync(chatId, message, cancellationToken);
                    break;

                case "WaitingForBudget":
                    await HandleWaitingForBudgetAsync(chatId, message, cancellationToken);
                    break;

                case "WaitingForGameSelection":
                    await HandleGameSelectionAsync(chatId, message, cancellationToken);
                    break;

                case "WaitingForRemoveId":
                    await HandleRemoveFromWishlistAsync(chatId, message, cancellationToken);
                    break;

                case "WaitingForUnsubscribeId":
                    await HandleUnsubscribeGameAsync(chatId, message, cancellationToken);
                    break;

                default:
                    ClearState(chatId);
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❗ Сталася помилка зі станом. Повертаємося в меню.",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;
            }
        }

        #region Private helpers

        private async Task HandleWaitingForNameAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            string nameQuery = message.Trim();

            // 1) Якщо користувач не ввів нічого або ввів «⬅️ Назад», оброблюємо це
            if (string.IsNullOrEmpty(nameQuery))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗️Будь ласка, введіть назву гри хоча б із одного символу.",
                    cancellationToken: cancellationToken
                );
                return;
            }
            if (nameQuery == "⬅️ Назад")
            {
                ClearState(chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🔙 Повернулись у головне меню.",
                    replyMarkup: KeyboardManager.GetMainKeyboard(),
                    cancellationToken: cancellationToken
                );
                return;
            }

            // 2) Відправляємо попередження «Йде пошук…»
            var loadingMsg = await _botClient.SendMessage(
                chatId: chatId,
                text: "🔎 Йде пошук… Будь ласка, зачекайте.",
                cancellationToken: cancellationToken
            );

            List<GameSearchResult>? games = null;
            try
            {
                games = await _apiClient.SearchGamesAsync(nameQuery);
            }
            catch (HttpRequestException)
            {
                // 3) Якщо сталася помилка під час звернення до сервера – видаляємо «Йде пошук…» і показуємо помилку
                await _botClient.DeleteMessage(chatId, loadingMsg.MessageId, cancellationToken);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗️Не вдалося зв’язатися з сервером пошуку. Спробуйте ще раз ввести коректну назву гри.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            // 4) Наскільки б не було результатів — спочатку видаляємо «Йде пошук…»
            await _botClient.DeleteMessage(chatId, loadingMsg.MessageId, cancellationToken);

            // 5) Якщо не знайдено жодної гри – повідомляємо користувача
            if (games == null || !games.Any())
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"😕 Ігор із назвою «<b>{Escape(nameQuery)}</b>» не знайдено.\n\n"
                        + "Спробуйте іншу назву або перевірте правопис.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: cancellationToken
                );
                return;
            }

            // 6) Якщо знайшли – формуємо ReplyKeyboardMarkup з результатами
            var kb = new ReplyKeyboardMarkup(
                games.Select(g => new[] { new KeyboardButton($"{g.Name} (ID: {g.Id})") }).ToArray()
            )
            {
                ResizeKeyboard = true,
            };

            await _botClient.SendMessage(
                chatId: chatId,
                text: "🎯 Ось результати пошуку. Оберіть гру з опису:",
                replyMarkup: kb,
                cancellationToken: cancellationToken
            );

            _userStates[chatId] = "WaitingForGameSelection";
            _gameKeyboards[chatId] = kb;
        }

        public static readonly string[] RecognizedGenres = new[]
        {
            "Action",
            "Strategy",
            "RPG",
            "Indie",
            "Adventure",
            "Simulation",
            "Sports",
            "Racing",
            "MMO",
            "Early Acces",
            "Free",
        };

        private async Task HandleWaitingForGenreAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            var genreSearch = message.Trim();

            // 1) Перевіряємо, чи запит порожній або занадто короткий
            if (string.IsNullOrWhiteSpace(genreSearch) || genreSearch.Length < 2)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗️Будь ласка, введіть жанр хоча б з 2 символів, наприклад: RPG або Action.\n\n"
                        + "СПИСОК ПОПУЛЯРНИХ ЖАНРІВ:\n"
                        + string.Join(", ", RecognizedGenres.Select(g => $"• {g}"))
                        + "\n\nСпробуйте ще раз.",
                    cancellationToken: cancellationToken
                );
                return; // залишаємо стан “WaitingForGenre”
            }

            // 2) Відправляємо проміжне повідомлення «Йде пошук…» і зберігаємо його ID
            var loadingMsg = await _botClient.SendMessage(
                chatId: chatId,
                text: "🔎 Йде пошук ігор за жанром… Будь ласка, зачекайте.",
                cancellationToken: cancellationToken
            );

            List<GameSearchResult> genreGames;
            try
            {
                // 3) Викликаємо API для пошуку за жанром
                genreGames = await _apiClient.GetGamesByGenreSpyAsync(genreSearch);
            }
            catch (HttpRequestException)
            {
                // 4) Якщо сталася помилка, видаляємо “йде пошук” і повідомляємо про помилку
                await _botClient.DeleteMessage(
                    chatId: chatId,
                    messageId: loadingMsg.MessageId,
                    cancellationToken: cancellationToken
                );

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗️Помилка при зверненні до сервера. Спробуйте, будь ласка, ще раз ввести коректний жанр.",
                    cancellationToken: cancellationToken
                );
                return; // залишаємо стан “WaitingForGenre”
            }

            // 5) Після отримання результатів завжди видаляємо “йде пошук”
            await _botClient.DeleteMessage(
                chatId: chatId,
                messageId: loadingMsg.MessageId,
                cancellationToken: cancellationToken
            );

            // 6) Якщо нічого не знайдено – повідомляємо користувача з підказкою
            if (genreGames == null || !genreGames.Any())
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"😕 Ігор у жанрі \"<b>{Escape(genreSearch)}</b>\" не знайдено.\n\n"
                        + "Можливо, ви ввели некоректний жанр. Ось деякі з популярних жанрів:\n"
                        + string.Join(", ", RecognizedGenres.Select(g => $"• {g}"))
                        + "\n\nСпробуйте ввести один з них.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: cancellationToken
                );
                return; // залишаємо стан “WaitingForGenre”
            }

            // 7) Якщо знайдено результати – формуємо клавіатуру і надсилаємо користувачу
            var genreButtons = genreGames
                .Take(10)
                .Select(g => new[] { new KeyboardButton($"{g.Name} (ID: {g.Id})") })
                .ToArray();

            var kb = new ReplyKeyboardMarkup(genreButtons) { ResizeKeyboard = true };

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"🎮 Ось кілька ігор у жанрі <b>{Escape(genreSearch)}</b>:",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: kb,
                cancellationToken: cancellationToken
            );

            _userStates[chatId] = "WaitingForGameSelection";
            _gameKeyboards[chatId] = kb;
        }

        private async Task HandleWaitingForBudgetAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            string text = message.Trim();

            // 1) Обробка «Назад»
            if (text == "⬅️ Назад")
            {
                ClearState(chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🔙 Повернулися до головного меню.",
                    replyMarkup: KeyboardManager.GetMainKeyboard(),
                    cancellationToken: cancellationToken
                );
                return;
            }

            // 2) Спроба розпарсити бюджет у число
            if (
                !double.TryParse(
                    text.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var maxDollars
                )
                || double.IsInfinity(maxDollars)
                || maxDollars < 0
            )
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗ Неправильний формат бюджету. Введіть число від 0 до 100 (0 – безкоштовні).",
                    cancellationToken: cancellationToken
                );
                return; // залишаємо стан “WaitingForBudget”
            }

            // 3) Перевірка межі 100
            if (maxDollars > 100)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "💡 У Steam майже не буває ігор дорожчих за 70 $. Введіть бюджет 0–100.",
                    cancellationToken: cancellationToken
                );
                return; // залишаємо стан “WaitingForBudget”
            }

            // 4) Відправляємо повідомлення «Йде пошук…»
            var loadingMsg = await _botClient.SendMessage(
                chatId: chatId,
                text: "💰 Йде пошук ігор за зазначеним бюджетом… Зачекайте, будь ласка.",
                cancellationToken: cancellationToken
            );

            List<GameSearchResult> budgetGames;

            try
            {
                if (Math.Abs(maxDollars) < 0.0001)
                {
                    // Якщо бюджет = 0, шукаємо ≤ 0.01, потім фільтруємо тільки безкоштовні
                    var maybeList = await _apiClient.GetGamesByBudgetSpyAsync(0.01);
                    budgetGames =
                        maybeList?.Where(g => g.Price == 0).ToList()
                        ?? new List<GameSearchResult>();
                }
                else
                {
                    // Інакше – звичайний запит «≤ maxDollars»
                    budgetGames = await _apiClient.GetGamesByBudgetSpyAsync(maxDollars);
                }
            }
            catch (HttpRequestException)
            {
                // 5) Помилка на стороні API – спершу видаляємо «Йде пошук…», потім повідомляємо помилку
                await _botClient.DeleteMessage(chatId, loadingMsg.MessageId, cancellationToken);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗ Помилка при зверненні до сервера пошуку за бюджетом. Спробуйте ще раз ввести коректний бюджет.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            // 6) Будь-як – видаляємо «Йде пошук…»
            await _botClient.DeleteMessage(chatId, loadingMsg.MessageId, cancellationToken);

            // 7) Якщо не знайдено жодної гри
            if (budgetGames == null || !budgetGames.Any())
            {
                var hint =
                    maxDollars < 0.0001
                        ? "😕 Безкоштовні ігри тимчасово не знайдено."
                        : $"😕 Ігор із ціною до ${maxDollars:0.##} не знайдено.";
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: hint + "\nСпробуйте інший бюджет 0–100 $.",
                    cancellationToken: cancellationToken
                );
                return; // залишаємо стан “WaitingForBudget”
            }

            // 8) Формуємо клавіатуру з перших 10 знайдених ігор
            var gameButtons = budgetGames
                .Take(10)
                .Select(g =>
                {
                    var priceFormatted =
                        g.Price == 0 ? " – Безкоштовно" : $" – {g.Price / 100.0:0.00}$";
                    return new KeyboardButton($"{g.Name} (ID: {g.Id}){priceFormatted}");
                })
                .Select(b => new[] { b })
                .ToArray();

            var kb = new ReplyKeyboardMarkup(gameButtons) { ResizeKeyboard = true };
            _gameKeyboards[chatId] = kb;

            var headerText =
                maxDollars < 0.0001
                    ? "🎮 Ось безкоштовні ігри:"
                    : $"💵 Ігри у бюджеті до ${maxDollars:0.##}:";

            await _botClient.SendMessage(
                chatId: chatId,
                text: headerText,
                replyMarkup: kb,
                cancellationToken: cancellationToken
            );

            _userStates[chatId] = "WaitingForGameSelection";
        }

        private async Task HandleGameSelectionAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            if (_userMessageToDelete.TryGetValue(chatId, out var msgId))
            {
                _userMessageToDelete.Remove(chatId);
            }

            if (message == "⬅️ Назад")
            {
                ClearState(chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🔙 Повернулися до головного меню.",
                    replyMarkup: KeyboardManager.GetMainKeyboard(),
                    cancellationToken: cancellationToken
                );
                return;
            }

            var match = Regex.Match(message, @"\(ID:\s*(\d+)\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var appId))
            {
                await SendGameDetailsAsync(chatId, appId, cancellationToken);
                ClearState(chatId);
            }
            else
            {
                if (_gameKeyboards.TryGetValue(chatId, out var prevKb))
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❗ Будь ласка, оберіть гру зі списку, натиснувши на одну з кнопок нижче. Якщо ви не бачите потрібної гри, натисніть «⬅️ Назад» і спробуйте інший пошук.",
                        replyMarkup: prevKb,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    ClearState(chatId);
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❗ Виникла помилка. Повертаємося в головне меню.",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                }
            }
        }

        private async Task HandleRemoveFromWishlistAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            if (message == "⬅️ Назад")
            {
                ClearState(chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🔙 Повернулися до головного меню.",
                    replyMarkup: KeyboardManager.GetMainKeyboard(),
                    cancellationToken: cancellationToken
                );
                return;
            }

            if (!int.TryParse(message.Trim(), out var removeId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗ Будь ласка, введіть лише число (ID гри) або натисніть «⬅️ Назад» для відміни.",
                    replyMarkup: new ReplyKeyboardMarkup(
                        new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                    )
                    {
                        ResizeKeyboard = true,
                    },
                    cancellationToken: cancellationToken
                );
                return;
            }

            var userSettings = await _userService.GetSettingsAsync(chatId);
            if (!userSettings.Wishlist.Contains(removeId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"⚠️ Гри з ID {removeId} немає в вашому вішлісті.\n\n"
                        + "Перевірте правильність ID або натисніть «⬅️ Назад» для повернення до меню.",
                    replyMarkup: new ReplyKeyboardMarkup(
                        new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                    )
                    {
                        ResizeKeyboard = true,
                    },
                    cancellationToken: cancellationToken
                );
                return;
            }

            await _userService.RemoveFromWishlistAsync(chatId, removeId);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Успішно видалено гру з ID {removeId} з вішліста.",
                replyMarkup: KeyboardManager.GetMainKeyboard(),
                cancellationToken: cancellationToken
            );

            ClearState(chatId);
        }

        private async Task HandleUnsubscribeGameAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            if (message == "⬅️ Назад")
            {
                ClearState(chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🔙 Повернулися до головного меню.",
                    replyMarkup: KeyboardManager.GetMainKeyboard(),
                    cancellationToken: cancellationToken
                );
                return;
            }

            if (!int.TryParse(message.Trim(), out var unsubId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗ Введіть, будь ласка, лише число (ID гри) або натисніть «⬅️ Назад» для відміни.",
                    replyMarkup: new ReplyKeyboardMarkup(
                        new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                    )
                    {
                        ResizeKeyboard = true,
                    },
                    cancellationToken: cancellationToken
                );
                return;
            }

            var user = await _userService.GetSettingsAsync(chatId);
            if (!user.SubscribedGames.Contains(unsubId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"⚠️ Ви не були підписані на гру з ID {unsubId}. Перевірте ID або натисніть «⬅️ Назад» для повернення до меню.",
                    replyMarkup: new ReplyKeyboardMarkup(
                        new[] { new[] { new KeyboardButton("⬅️ Назад") } }
                    )
                    {
                        ResizeKeyboard = true,
                    },
                    cancellationToken: cancellationToken
                );
                return;
            }

            user.SubscribedGames.Remove(unsubId);
            await _apiClient.UpdateUserSettingsAsync(user);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"🔕 Ви успішно відписалися від новин гри з ID {unsubId}.",
                replyMarkup: KeyboardManager.GetMainKeyboard(),
                cancellationToken: cancellationToken
            );

            ClearState(chatId);
        }

        private async Task SendGameDetailsAsync(
            long chatId,
            int appId,
            CancellationToken cancellationToken
        )
        {
            var settings = await _userService.GetSettingsAsync(chatId);
            var wishlist = settings.Wishlist;
            var data = await _apiClient.GetGameDetailsAsync(appId);

            if (
                data == null
                || !data.TryGetValue("data", out var raw)
                || !(raw is JsonElement json)
            )
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❗ Не вдалося завантажити дані про гру.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            var details = GameDetails.FromJson(json, appId, wishlist);
            var subscribed = settings.SubscribedGames ?? new List<int>();

            await _botClient.SendMessage(
                chatId: chatId,
                text: details.ToHtmlCaption(),
                parseMode: ParseMode.Html,
                replyMarkup: details.ToInlineKeyboard("UA", subscribed),
                cancellationToken: cancellationToken
            );

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Що далі?",
                replyMarkup: KeyboardManager.GetMainKeyboard(),
                cancellationToken: cancellationToken
            );
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        #endregion
    }
}
