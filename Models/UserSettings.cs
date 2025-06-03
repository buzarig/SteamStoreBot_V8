using System.Collections.Generic;

namespace SteamStoreBot_V8.Models
{
    public class UserSettings
    {
        public long ChatId { get; set; }
        public List<int> Wishlist { get; set; } = new List<int>();
        public bool SubscriptionOnSales { get; set; }
        public List<int> SubscribedGames { get; set; } = new List<int>();
    }
}
