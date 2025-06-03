using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot_V8.Models
{
    public class GameDetails
    {
        public int AppId { get; set; }

        // Назва гри: гарантовано непуста (якщо в JSON немає "name", використовуємо "")
        public string Name { get; set; } = string.Empty;

        // Текст ціни: також гарантовано непуста стрічка
        public string PriceText { get; set; } = "Недоступна";

        // Короткий опис: мінімум ""
        public string ShortDescription { get; set; } = string.Empty;

        // Мінімальні вимоги: також мінімум ""
        public string MinRequirements { get; set; } = string.Empty;

        public bool HasUaLocalization { get; set; }

        // Наприклад, "-" або число як рядок
        public string MetacriticScore { get; set; } = "-";

        public int ReviewsCount { get; set; }

        // Список жанрів ніколи не лишиться null
        public List<string> Genres { get; set; } = new List<string>();

        // Стрічка-хештеги, гарантовано непуста (може бути "")
        public string Hashtags { get; set; } = string.Empty;

        // Посилання на Steam: гарантовано у форматі "https://…"
        public string StoreUrl { get; set; } = string.Empty;

        // Трейлер може бути відсутнім → дозволяємо null
        public string? TrailerUrl { get; set; }

        public bool IsInWishlist { get; set; }

        public static GameDetails FromJson(
            JsonElement dataJson,
            int appId,
            IEnumerable<int> wishlistGameIds
        )
        {
            var details = new GameDetails { AppId = appId };

            // ---- Обробка priceText ----
            if (
                dataJson.TryGetProperty("price_overview", out var priceOverview)
                && priceOverview.ValueKind == JsonValueKind.Object
            )
            {
                if (priceOverview.TryGetProperty("final_formatted", out var finalProp))
                {
                    // GetString() повертає string?, тому робимо ?? string.Empty
                    var final = finalProp.GetString() ?? string.Empty;

                    var original = priceOverview.TryGetProperty("initial_formatted", out var o)
                        ? o.GetString() ?? string.Empty
                        : string.Empty;

                    var discount = priceOverview.TryGetProperty("discount_percent", out var d)
                        ? d.GetInt32()
                        : 0;

                    if (!string.IsNullOrEmpty(final))
                    {
                        details.PriceText =
                            discount > 0 && !string.IsNullOrEmpty(original)
                                ? $"{original} ➔ {final} (-{discount}%)"
                                : final;
                    }
                }
            }
            else if (
                dataJson.TryGetProperty("is_free", out var isFreeProp)
                && isFreeProp.ValueKind == JsonValueKind.True
            )
            {
                details.PriceText = "Безкоштовно";
            }

            // ---- Обробка короткого опису ----
            if (dataJson.TryGetProperty("short_description", out var descProp))
            {
                // Якщо GetString() повернуло null → беремо ""
                details.ShortDescription = descProp.GetString() ?? string.Empty;
            }

            // ---- Обробка мінімальних вимог ----
            string minReq = string.Empty;
            if (
                dataJson.TryGetProperty("pc_requirements", out var reqs)
                && reqs.TryGetProperty("minimum", out var min)
            )
            {
                var minHtml = min.GetString() ?? string.Empty;
                minReq = Regex.Replace(minHtml, "<.*?>", string.Empty).Trim();
            }
            details.MinRequirements = minReq;

            // ---- Обробка локалізації ----
            var langs = dataJson.TryGetProperty("supported_languages", out var langProp)
                ? langProp.GetString() ?? string.Empty
                : string.Empty;
            langs = Regex.Replace(langs, "<.*?>", string.Empty);
            details.HasUaLocalization =
                langs.IndexOf("українська", StringComparison.OrdinalIgnoreCase) >= 0;

            // ---- Обробка Metacritic ----
            if (
                dataJson.TryGetProperty("metacritic", out var meta)
                && meta.TryGetProperty("score", out var scoreProp)
            )
            {
                // scoreProp.GetInt32() дає int, перетворюємо на рядок
                details.MetacriticScore = scoreProp.GetInt32().ToString();
            }
            else
            {
                details.MetacriticScore = "-";
            }

            // ---- Обробка кількості відгуків ----
            if (
                dataJson.TryGetProperty("recommendations", out var rec)
                && rec.TryGetProperty("total", out var total)
            )
            {
                details.ReviewsCount = total.GetInt32();
            }
            else
            {
                details.ReviewsCount = 0;
            }

            // ---- Обробка жанрів ----
            var genres = new List<string>();
            if (
                dataJson.TryGetProperty("genres", out var genreArray)
                && genreArray.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var item in genreArray.EnumerateArray())
                {
                    if (item.TryGetProperty("description", out var g))
                    {
                        var gName = g.GetString();
                        if (!string.IsNullOrEmpty(gName))
                            genres.Add(gName);
                    }
                }
            }
            details.Genres = genres;

            // ---- Обробка категорій (для хештегів) ----
            var categoryDescriptions = new List<string>();
            if (
                dataJson.TryGetProperty("categories", out var categoriesJson)
                && categoriesJson.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var item in categoriesJson.EnumerateArray())
                {
                    if (item.TryGetProperty("description", out var d))
                    {
                        // d.GetString може повернути null, тому ?? ""
                        var desc = d.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(desc))
                            categoryDescriptions.Add(desc);
                    }
                }
            }

            // ---- Формування хештегів ----
            var genreTags = details
                .Genres.Select(g => "#" + Regex.Replace(g.ToLower(), "[^a-z0-9]", string.Empty))
                .Where(tag => tag.Length > 1);

            var categoryTags = categoryDescriptions
                .Select(c => "#" + Regex.Replace(c.ToLower(), "[^a-z0-9]", string.Empty))
                .Where(tag => tag.Length > 1);

            details.Hashtags = string.Join(" ", genreTags.Concat(categoryTags).Distinct());

            // ---- Посилання на сторінку гри у Steam ----
            details.StoreUrl = $"https://store.steampowered.com/app/{appId}";

            // ---- Обробка трейлера (може бути null) ----
            string? trailer = null;
            if (
                dataJson.TryGetProperty("movies", out var movies)
                && movies.ValueKind == JsonValueKind.Array
                && movies.GetArrayLength() > 0
            )
            {
                if (
                    movies[0].TryGetProperty("mp4", out var mp4)
                    && mp4.TryGetProperty("max", out var urlProp)
                )
                {
                    // urlProp.GetString() може повернути null або рядок
                    trailer = urlProp.GetString();
                }
            }
            // Якщо trailer залишився null, властивість TrailerUrl віддасть null
            details.TrailerUrl = trailer;

            // ---- Чи є гра у вішлисті ----
            details.IsInWishlist = wishlistGameIds.Contains(appId);

            // ---- Ім’я гри (обов’язково) ----
            if (dataJson.TryGetProperty("name", out var nameProp))
            {
                details.Name = nameProp.GetString() ?? string.Empty;
            }
            else
            {
                details.Name = string.Empty;
            }

            return details;
        }

        public string ToHtmlCaption()
        {
            // Переконаємося, що MinRequirements не null
            // (але за лічильником вище MinRequirements вже завжди має бути хоча б "")
            var minReq = MinRequirements?.Trim() ?? string.Empty;

            // Видалимо префікс “Мінімальні:” або “Мін. вимоги:”
            const string prefix1 = "Мінімальні:";
            if (minReq.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase))
            {
                minReq = minReq.Substring(prefix1.Length).Trim();
            }

            const string prefix2 = "Мін. вимоги:";
            if (minReq.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
            {
                minReq = minReq.Substring(prefix2.Length).Trim();
            }

            var lines = new List<string>
            {
                $"🎮 <b>Гра:</b> {Escape(Name)}",
                "",
                $"💰 <b>Ціна:</b> {Escape(PriceText)}",
                "",
                $"📝 <b>Опис:</b> {Escape(ShortDescription)}",
                "",
            };

            // Додаємо блок мінімальних вимог лише якщо він непустий
            if (!string.IsNullOrWhiteSpace(minReq))
            {
                lines.Add($"🖥️ <b>Мін. вимоги:</b> {Escape(minReq)}");
                lines.Add("");
            }

            lines.Add($"🌐 <b>Локалізація UA:</b> {(HasUaLocalization ? "✅" : "❌")}");
            lines.Add("");
            lines.Add($"⭐ <b>Metacritic:</b> {Escape(MetacriticScore)}");
            lines.Add($"💬 <b>Відгуки:</b> {ReviewsCount} user ratings");
            lines.Add("");
            lines.Add($"📂 <b>Жанри:</b> {Escape(string.Join(", ", Genres))}");
            lines.Add($"🔖 {Hashtags}");

            return string.Join("\n", lines);
        }

        public InlineKeyboardMarkup ToInlineKeyboard(
            string currency = "UA",
            IEnumerable<int>? subscribedGameIds = null
        )
        {
            var buttons = new List<InlineKeyboardButton[]>
            {
                new[] { InlineKeyboardButton.WithUrl("🔗 Відкрити в Steam", StoreUrl) },
            };

            // Якщо є трейлер (TrailerUrl != null), додаємо кнопку перегляду
            if (!string.IsNullOrEmpty(TrailerUrl))
            {
                buttons.Add(
                    new[] { InlineKeyboardButton.WithUrl("🎞 Переглянути трейлер", TrailerUrl!) }
                );
            }

            // Кнопка “у вішлісті” / “додати у вішліст”
            var wishlistBtn = IsInWishlist
                ? InlineKeyboardButton.WithCallbackData("✅ У вішлісті", "noop")
                : InlineKeyboardButton.WithCallbackData(
                    "➕ Вішліст",
                    $"addwishlist:{AppId}:{currency.ToLowerInvariant()}"
                );
            buttons.Add(new[] { wishlistBtn });

            // Кнопка підписки на новини
            var isSubscribed = subscribedGameIds?.Contains(AppId) == true;
            var subscribeBtn = isSubscribed
                ? InlineKeyboardButton.WithCallbackData(
                    "🔕 Скасувати підписку",
                    $"unsubscribe_news:{AppId}:{currency}"
                )
                : InlineKeyboardButton.WithCallbackData(
                    "🔔 Підписатись на новини",
                    $"subscribe_news:{AppId}:{currency}"
                );
            buttons.Add(new[] { subscribeBtn });

            // Якщо гра платна (ціна не містить "Недоступна", "Free" або "безкоштовно"),
            // додаємо кнопку конвертації валюти
            if (
                PriceText.IndexOf("Недоступна", StringComparison.OrdinalIgnoreCase) < 0
                && PriceText.IndexOf("Free", StringComparison.OrdinalIgnoreCase) < 0
                && PriceText.IndexOf("безкоштовно", StringComparison.OrdinalIgnoreCase) < 0
            )
            {
                if (currency.Equals("UA", StringComparison.OrdinalIgnoreCase))
                {
                    buttons.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💲 Показати ціну в $",
                                $"convert_to_usd_{AppId}"
                            ),
                        }
                    );
                }
                else
                {
                    buttons.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💴 Показати в грн",
                                $"convert_to_uah_{AppId}"
                            ),
                        }
                    );
                }
            }

            return new InlineKeyboardMarkup(buttons);
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
