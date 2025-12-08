namespace UniversityTinder.Models.Dto
{
    public class SwipeResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Match oldu mu?
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// Match olan kullanıcı bilgisi (match olduysa)
        /// </summary>
        public UserDto? MatchedUser { get; set; }

        /// <summary>
        /// Kalan günlük swipe hakkı (free users için)
        /// </summary>
        public int? RemainingSwipes { get; set; }

        /// <summary>
        /// Kalan super like hakkı
        /// </summary>
        public int? RemainingSuperLikes { get; set; }

        /// <summary>
        /// Premium paywall gösterilsin mi?
        /// </summary>
        public bool ShowPaywall { get; set; }

        /// <summary>
        /// Paywall tipi: "SWIPE_LIMIT", "SUPER_LIKE_LIMIT"
        /// </summary>
        public string? PaywallType { get; set; }

        /// <summary>
        /// Paywall mesajı
        /// </summary>
        public string? PaywallMessage { get; set; }
    }
}
