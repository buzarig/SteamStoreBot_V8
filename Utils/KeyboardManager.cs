// Utils/KeyboardManager.cs
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot_V8.Utils
{
    internal static class KeyboardManager
    {
        public static ReplyKeyboardMarkup GetMainKeyboard()
        {
            return new ReplyKeyboardMarkup(
                [
                    [new KeyboardButton("📜 Список бажань")],
                    [new KeyboardButton("🔎 Пошук ігор")],
                    [new KeyboardButton("📰 Підписка на новини")],
                    [new KeyboardButton("🔥 Щоденні знижки")],
                ]
            )
            {
                ResizeKeyboard = true,
            };
        }

        public static ReplyKeyboardMarkup GetSearchKeyboard()
        {
            return new ReplyKeyboardMarkup(
                [
                    [new KeyboardButton("🖊️ Пошук по назві")],
                    [new KeyboardButton("📚 Пошук по жанру")],
                    [new KeyboardButton("💰 Пошук по бюджету")],
                    [new KeyboardButton("⬅️ Назад")],
                ]
            )
            {
                ResizeKeyboard = true,
            };
        }

        public static ReplyKeyboardMarkup GetWishlistKeyboard()
        {
            return new ReplyKeyboardMarkup(
                [
                    [new KeyboardButton("❌ Видалити з вішліста")],
                    [new KeyboardButton("⬅️ Назад")],
                ]
            )
            {
                ResizeKeyboard = true,
            };
        }

        public static ReplyKeyboardMarkup GetSubscriptionKeyboard()
        {
            return new ReplyKeyboardMarkup(
                [
                    [new KeyboardButton("❌ Відписатися від новин")],
                    [new KeyboardButton("⬅️ Назад")],
                ]
            )
            {
                ResizeKeyboard = true,
            };
        }
    }
}
