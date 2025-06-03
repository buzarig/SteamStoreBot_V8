using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace SteamStoreBot_V8.Interfaces
{
    public interface ICommandHandler
    {
        Task HandleAsync(Update update, CancellationToken cancellationToken);
    }
}
