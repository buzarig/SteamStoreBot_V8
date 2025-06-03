using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamStoreBot_V8.Interfaces;
using SteamStoreBot_V8.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot_V8.Services
{
    internal class CallbackHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ApiClient _apiClient;
        private readonly IUserService _userService;

        public CallbackHandler(
            ITelegramBotClient botClient,
            ApiClient apiClient,
            IUserService userService
        )
        {
            _botClient = botClient;
            _apiClient = apiClient;
            _userService = userService;
        }

        public async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken cancellationToken)
        {
            if (cb.Message is null || cb.Data is null)
            {
                // Можна, наприклад, просто повернутися:
                return;
                // Або кинути виняток, або логнути щось – залежить від бізнес-логіки:
                // throw new InvalidOperationException("CallbackQuery.Message is null");
            }
            var chatId = cb.Message.Chat.Id;
            var messageId = cb.Message.MessageId;
            var data = cb.Data;

            // ─── Додавання/видалення з вішліста ───
            if (data.StartsWith("addwishlist:"))
            {
                var parts = data.Split(':');
                if (
                    parts.Length == 3
                    && int.TryParse(parts[1], out var appId)
                    && !string.IsNullOrWhiteSpace(parts[2])
                )
                {
                    var currency = parts[2].ToUpper();
                    await _userService.AddToWishlistAsync(chatId, appId);

                    var settings = await _userService.GetSettingsAsync(chatId);
                    var details = await _apiClient.GetGameDetailsAsync(
                        appId,
                        currency,
                        "ukrainian"
                    );
                    if (
                        details != null
                        && details.TryGetValue("data", out var raw)
                        && raw is JsonElement json
                    )
                    {
                        var gameDetails = GameDetails.FromJson(json, appId, settings.Wishlist);
                        var updatedMarkup = gameDetails.ToInlineKeyboard(
                            currency,
                            settings.SubscribedGames
                        );

                        await _botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: gameDetails.ToHtmlCaption(),
                            parseMode: ParseMode.Html,
                            replyMarkup: updatedMarkup,
                            cancellationToken: cancellationToken
                        );
                        await _botClient.AnswerCallbackQuery(cb.Id, "✅ Додано у вішліст!");
                    }
                }
                return;
            }
            if (data.StartsWith("removewishlist:"))
            {
                var parts = data.Split(':');
                if (
                    parts.Length == 3
                    && int.TryParse(parts[1], out var appId)
                    && !string.IsNullOrWhiteSpace(parts[2])
                )
                {
                    var currency = parts[2].ToUpper();
                    await _userService.RemoveFromWishlistAsync(chatId, appId);

                    var settings = await _userService.GetSettingsAsync(chatId);
                    var details = await _apiClient.GetGameDetailsAsync(
                        appId,
                        currency,
                        "ukrainian"
                    );
                    if (
                        details != null
                        && details.TryGetValue("data", out var raw)
                        && raw is JsonElement json
                    )
                    {
                        var gameDetails = GameDetails.FromJson(json, appId, settings.Wishlist);
                        var updatedMarkup = gameDetails.ToInlineKeyboard(
                            currency,
                            settings.SubscribedGames
                        );

                        await _botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: gameDetails.ToHtmlCaption(),
                            parseMode: ParseMode.Html,
                            replyMarkup: updatedMarkup,
                            cancellationToken: cancellationToken
                        );
                        await _botClient.AnswerCallbackQuery(cb.Id, "✅ Видалено з вішліста!");
                    }
                }
                return;
            }

            // ─── Підписка / відписка від новин ───
            if (data.StartsWith("subscribe_news:") || data.StartsWith("unsubscribe_news:"))
            {
                var parts = data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[1], out var appId))
                {
                    var action = parts[0];
                    var currency = parts[2];

                    var settings = await _userService.GetSettingsAsync(chatId);

                    if (action == "subscribe_news")
                    {
                        await _userService.SubscribeToGameNewsAsync(chatId, appId);
                        settings = await _userService.GetSettingsAsync(chatId);

                        var details = await _apiClient.GetGameDetailsAsync(
                            appId,
                            currency,
                            "ukrainian"
                        );
                        if (
                            details != null
                            && details.TryGetValue("data", out var raw)
                            && raw is JsonElement json
                        )
                        {
                            var game = GameDetails.FromJson(json, appId, settings.Wishlist);
                            var newMarkup = game.ToInlineKeyboard(
                                currency,
                                settings.SubscribedGames
                            );

                            await _botClient.EditMessageReplyMarkup(
                                chatId: chatId,
                                messageId: messageId,
                                replyMarkup: newMarkup,
                                cancellationToken: cancellationToken
                            );
                            await _botClient.AnswerCallbackQuery(cb.Id, "🔔 Підписка активована!");
                        }
                        return;
                    }
                    else // unsubscribe_news
                    {
                        if (settings.SubscribedGames.Contains(appId))
                        {
                            settings.SubscribedGames.Remove(appId);
                            await _userService.UpdateUserSettingsAsync(settings);

                            var details = await _apiClient.GetGameDetailsAsync(
                                appId,
                                currency,
                                "ukrainian"
                            );
                            if (
                                details != null
                                && details.TryGetValue("data", out var raw2)
                                && raw2 is JsonElement json2
                            )
                            {
                                var game = GameDetails.FromJson(json2, appId, settings.Wishlist);
                                var newMarkup = game.ToInlineKeyboard(
                                    currency,
                                    settings.SubscribedGames
                                );

                                await _botClient.EditMessageReplyMarkup(
                                    chatId: chatId,
                                    messageId: messageId,
                                    replyMarkup: newMarkup,
                                    cancellationToken: cancellationToken
                                );
                                await _botClient.AnswerCallbackQuery(
                                    cb.Id,
                                    "🔕 Підписку скасовано"
                                );
                            }
                        }
                        else
                        {
                            await _botClient.AnswerCallbackQuery(cb.Id, "Ви не були підписані");
                        }
                        return;
                    }
                }
            }

            // ─── Конвертація цін ───
            if (data.StartsWith("convert_to_usd_") || data.StartsWith("convert_to_uah_"))
            {
                if (
                    data.StartsWith("convert_to_usd_")
                    && int.TryParse(data.Substring("convert_to_usd_".Length), out var appIdUsd)
                )
                {
                    var settings = await _userService.GetSettingsAsync(chatId);
                    var detailsUsd = await _apiClient.GetGameDetailsAsync(
                        appIdUsd,
                        "US",
                        "ukrainian"
                    );
                    if (
                        detailsUsd != null
                        && detailsUsd.TryGetValue("data", out var rawUsd)
                        && rawUsd is JsonElement jsonUsd
                    )
                    {
                        var gameUsd = GameDetails.FromJson(jsonUsd, appIdUsd, settings.Wishlist);
                        var markupUsd = gameUsd.ToInlineKeyboard("US", settings.SubscribedGames);

                        await _botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: gameUsd.ToHtmlCaption(),
                            parseMode: ParseMode.Html,
                            replyMarkup: markupUsd,
                            cancellationToken: cancellationToken
                        );
                        await _botClient.AnswerCallbackQuery(cb.Id, "💲 Конвертовано в USD");
                    }
                }
                else if (
                    data.StartsWith("convert_to_uah_")
                    && int.TryParse(data.Substring("convert_to_uah_".Length), out var appIdUah)
                )
                {
                    var settings = await _userService.GetSettingsAsync(chatId);
                    var detailsUah = await _apiClient.GetGameDetailsAsync(
                        appIdUah,
                        "UA",
                        "ukrainian"
                    );
                    if (
                        detailsUah != null
                        && detailsUah.TryGetValue("data", out var rawUah)
                        && rawUah is JsonElement jsonUah
                    )
                    {
                        var gameUah = GameDetails.FromJson(jsonUah, appIdUah, settings.Wishlist);
                        var markupUah = gameUah.ToInlineKeyboard("UA", settings.SubscribedGames);

                        await _botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: gameUah.ToHtmlCaption(),
                            parseMode: ParseMode.Html,
                            replyMarkup: markupUah,
                            cancellationToken: cancellationToken
                        );
                        await _botClient.AnswerCallbackQuery(cb.Id, "💲 Конвертовано в UAH");
                    }
                }
                return;
            }

            // ─── Підписка/відписка на знижки ───
            if (data == "subscribe_sales" || data == "unsubscribe_sales")
            {
                var settings = await _userService.GetSettingsAsync(chatId);
                bool nowEnable = data == "subscribe_sales";
                await _userService.ToggleSalesSubscriptionAsync(chatId, nowEnable);

                settings = await _userService.GetSettingsAsync(chatId);
                var newInline = new InlineKeyboardMarkup(
                    new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                settings.SubscriptionOnSales
                                    ? "🔕 Відписатися від знижок"
                                    : "🔔 Підписатися на знижки",
                                settings.SubscriptionOnSales
                                    ? "unsubscribe_sales"
                                    : "subscribe_sales"
                            ),
                        },
                    }
                );

                await _botClient.EditMessageReplyMarkup(
                    chatId: chatId,
                    messageId: messageId,
                    replyMarkup: newInline,
                    cancellationToken: cancellationToken
                );

                if (nowEnable)
                    await _botClient.AnswerCallbackQuery(cb.Id, "✅ Підписка на знижки активована");
                else
                    await _botClient.AnswerCallbackQuery(cb.Id, "🔕 Підписку скасовано");

                return;
            }
        }
    }
}
