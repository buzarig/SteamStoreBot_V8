using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SteamStoreBot_V8.Models;

namespace SteamStoreBot_V8
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;

        // ------------------------------------------------------------------------
        // 1) Статичний кеш JsonSerializerOptions, щоб не створювати новий екземпляр щоразу
        // ------------------------------------------------------------------------
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Можна додати інші налаштування, якщо потрібно
        };

        public ApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ---------------------------------------------------------
        // Пошук ігор за назвою → завжди повертаємо ненульовий список
        // ---------------------------------------------------------
        public async Task<List<GameSearchResult>> SearchGamesAsync(string name)
        {
            var response = await _httpClient.GetAsync(
                $"api/search/games?name={Uri.EscapeDataString(name)}"
            );
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            // Десеріалізація може повернути null → замінимо на порожній список
            var result = JsonSerializer.Deserialize<List<GameSearchResult>>(json, _jsonOptions);
            return result ?? new List<GameSearchResult>();
        }

        // -----------------------------------------------------------------
        // Отримання деталей гри ⇒ повертаємо Dictionary (або порожній словник)
        // -----------------------------------------------------------------
        public async Task<Dictionary<string, object>> GetGameDetailsAsync(
            int appId,
            string cc = "UA",
            string lang = "ukrainian"
        )
        {
            var response = await _httpClient.GetAsync(
                $"api/search/details?appId={appId}&cc={cc}&l={lang}"
            );
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            // Десеріалізація може повернути null → замінимо на порожній словник
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
            return result ?? new Dictionary<string, object>();
        }

        // -------------------------------------------------------------------
        // Отримання налаштувань користувача. Якщо 404 → повертаємо новий екземпляр
        // -------------------------------------------------------------------
        public async Task<UserSettings> GetUserSettingsAsync(long chatId)
        {
            var resp = await _httpClient.GetAsync($"api/usersettings/{chatId}");
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                // Якщо юзер відсутній у базі, повертаємо пусті налаштування
                return new UserSettings { ChatId = chatId };
            }

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);

            // Якщо з якоїсь причини в тілі відповіді повернулось null, створимо новий обʼєкт
            return settings ?? new UserSettings { ChatId = chatId };
        }

        // ----------------------------------------
        // Оновлення налаштувань користувача у API
        // ----------------------------------------
        public async Task UpdateUserSettingsAsync(UserSettings settings)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(settings, _jsonOptions),
                Encoding.UTF8,
                "application/json"
            );
            var resp = await _httpClient.PutAsync($"api/usersettings/{settings.ChatId}", content);
            resp.EnsureSuccessStatusCode();
        }

        // -------------------------------------------------------------------------
        // Пошук ігор за жанром (spy-genre). Повертаємо ненульовий список (може бути пустим)
        // -------------------------------------------------------------------------
        public async Task<List<GameSearchResult>> GetGamesByGenreSpyAsync(string genre)
        {
            var url =
                $"api/search/spy-genre?genre={Uri.EscapeDataString(genre)}&minRating=90&minVotes=2000";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<List<GameSearchResult>>(json, _jsonOptions);
            return result ?? new List<GameSearchResult>();
        }

        // -------------------------------------------------------------------------------------
        // Пошук ігор за бюджетом (spy-budget). Якщо null → повертаємо порожній список
        // -------------------------------------------------------------------------------------
        public async Task<List<GameSearchResult>> GetGamesByBudgetSpyAsync(double maxDollars)
        {
            var url =
                $"api/search/spy-budget?max={maxDollars.ToString(System.Globalization.CultureInfo.InvariantCulture)}&minRating=70";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<List<GameSearchResult>>(json, _jsonOptions);
            return result ?? new List<GameSearchResult>();
        }

        // ----------------------------------------------------------------
        // Отримати ігри зі знижками (spy-discounts). Повертаємо ненульовий список
        // ----------------------------------------------------------------
        public async Task<List<GameSearchResult>> GetDiscountedGamesAsync()
        {
            var url = $"api/search/spy-discounts";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<List<GameSearchResult>>(json, _jsonOptions);
            return result ?? new List<GameSearchResult>();
        }

        // -------------------------------------------------------------------------
        // Отримання новин про гру (spy-news). Якщо щось піде не так — повертаємо пустий список
        // -------------------------------------------------------------------------
        public async Task<List<Dictionary<string, object>>> GetGameNewsAsync(int appId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/search/news?appId={appId}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                    json,
                    _jsonOptions
                );
                return result ?? new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                // Логування у консоль для дебагу
                Console.WriteLine($"❗ GetGameNewsAsync error: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        // ---------------------------------------------------------------------
        // Отримання списку всіх користувачів (для розсилки), повертаємо ненульовий список
        // ---------------------------------------------------------------------
        public async Task<List<UserSettings>> GetAllUsersAsync()
        {
            var resp = await _httpClient.GetAsync("api/usersettings");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<UserSettings>>(json, _jsonOptions);
            return result ?? new List<UserSettings>();
        }
    }
}
