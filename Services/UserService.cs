using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SteamStoreBot_V8.Interfaces;
using SteamStoreBot_V8.Models;

namespace SteamStoreBot_V8.Utils
{
    public class UserService : IUserService
    {
        private readonly ApiClient _api;
        private readonly ConcurrentDictionary<long, UserSettings> _cache =
            new ConcurrentDictionary<long, UserSettings>();

        public UserService(ApiClient api) => _api = api;

        public async Task<UserSettings> GetSettingsAsync(long chatId)
        {
            if (_cache.TryGetValue(chatId, out var s))
                return s;

            var settings = await _api.GetUserSettingsAsync(chatId);
            _cache[chatId] = settings;
            return settings;
        }

        public async Task AddToWishlistAsync(long chatId, int appId)
        {
            var s = await GetSettingsAsync(chatId);
            if (!s.Wishlist.Contains(appId))
            {
                Console.WriteLine(chatId.ToString(), appId);
                s.Wishlist.Add(appId);
                await _api.UpdateUserSettingsAsync(s);
            }
        }

        public async Task RemoveFromWishlistAsync(long chatId, int appId)
        {
            var s = await GetSettingsAsync(chatId);
            if (!s.Wishlist.Contains(appId))
                throw new InvalidOperationException(
                    $"Гра з ID {appId} відсутня у вашому вішлісті."
                );

            s.Wishlist.Remove(appId);
            await _api.UpdateUserSettingsAsync(s);
        }

        public async Task SubscribeToGameNewsAsync(long chatId, int appId)
        {
            var s = await GetSettingsAsync(chatId);

            if (!s.SubscribedGames.Contains(appId))
            {
                s.SubscribedGames.Add(appId);
                await _api.UpdateUserSettingsAsync(s);
            }
        }

        public async Task ToggleSalesSubscriptionAsync(long chatId, bool enable)
        {
            var s = await GetSettingsAsync(chatId);
            s.SubscriptionOnSales = enable;
            await _api.UpdateUserSettingsAsync(s);
        }

        public async Task UpdateUserSettingsAsync(UserSettings settings)
        {
            await _api.UpdateUserSettingsAsync(settings);
        }

        public async Task<List<UserSettings>> GetAllUsersAsync()
        {
            return await _api.GetAllUsersAsync();
        }
    }
}
