using System;
using System.Threading;
using System.Threading.Tasks;
using SteamStoreBot_V8.Interfaces;
using SteamStoreBot_V8.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SteamStoreBot_V8.Services
{
    internal class CommandHandler : ICommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TextCommandHandler _textHandler;
        private readonly StateHandler _stateHandler;
        private readonly CallbackHandler _callbackHandler;

        public CommandHandler(
            ITelegramBotClient botClient,
            TextCommandHandler textHandler,
            StateHandler stateHandler,
            CallbackHandler callbackHandler
        )
        {
            _botClient = botClient;
            _textHandler = textHandler;
            _stateHandler = stateHandler;
            _callbackHandler = callbackHandler;
        }

        public async Task HandleAsync(Update update, CancellationToken cancellationToken)
        {
            // Обробка CallbackQuery
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
            {
                await _callbackHandler.HandleCallbackAsync(update.CallbackQuery, cancellationToken);
                return;
            }

            // Обробка текстових повідомлень
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var chatId = update.Message.Chat.Id;
                var text = update.Message.Text.Trim();

                try
                {
                    // Якщо є поточний стан у користувача
                    if (_stateHandler.HasState(chatId))
                    {
                        await _stateHandler.HandleStateAsync(chatId, text, cancellationToken);
                    }
                    else
                    {
                        // Інакше – це нова команда
                        await _textHandler.HandleCommandTextAsync(chatId, text, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: $"❗ Сталася помилка: {ex.Message}",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
    }
}
