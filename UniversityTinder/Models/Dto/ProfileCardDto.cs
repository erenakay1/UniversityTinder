namespace UniversityTinder.Models.Dto
{
    /// <summary>
    /// Swipe ekranında gösterilecek profil kartı
    /// </summary>
    public class ProfileCardDto
    {
        public int ProfileId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Bio { get; set; }

        /// <summary>
        /// Profil fotoğrafları (sıralı)
        /// </summary>
        public List<string> Photos { get; set; } = new List<string>();

        // University & Education
        public string? UniversityName { get; set; }
        public string? Department { get; set; }
        public int? YearOfStudy { get; set; }

        // Distance & Status
        public int Distance { get; set; }  // km cinsinden
        public bool IsVerified { get; set; }
        public bool IsPremium { get; set; }

        // Privacy
        public bool ShowUniversity { get; set; }  // Premium veya user izin verdiyse göster

        /// <summary>
        /// Bu kişi beni beğendi mi? (Sadece premium kullanıcılar görebilir)
        /// </summary>
        public bool HasLikedMe { get; set; }

        /// <summary>
        /// Beni ne zaman beğendi? (Sadece premium için)
        /// </summary>
        public DateTime? LikedMeAt { get; set; }
    }
}