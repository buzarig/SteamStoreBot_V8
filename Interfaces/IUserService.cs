using SteamStoreBot_V8.Models;

namespace SteamStoreBot_V8.Interfaces
{
    public interface IUserService
    {
        Task<UserSettings> GetSettingsAsync(long chatId);
        Task AddToWishlistAsync(long chatId, int appId);
        Task RemoveFromWishlistAsync(long chatId, int appId);
        Task SubscribeToGameNewsAsync(long chatId, int appId);
        Task ToggleSalesSubscriptionAsync(long chatId, bool enable);
        Task UpdateUserSettingsAsync(UserSettings settings);
        Task<List<UserSettings>> GetAllUsersAsync();
    }
}
