using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using UniversityTinder.Models.Dto;
using System.Text.Json;

namespace UniversityTinder.Models
{
    public class UserProfile
    {
        [Key]
        public int ProfileId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        // Display & Bio
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Bio { get; set; }

        // Physical & Education
        public int? Height { get; set; }
        public string? Department { get; set; }
        public int? YearOfStudy { get; set; }

        // Profile Images
        public string? ProfileImageUrl { get; set; }
        public string? ProfileImageLocalPath { get; set; }

        // Location
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public DateTime? LastLocationUpdate { get; set; }

        // Dating Preferences
        public string InterestedIn { get; set; } = "Everyone";
        public int AgeRangeMin { get; set; } = 18;
        public int AgeRangeMax { get; set; } = 30;
        public int MaxDistance { get; set; } = 50;

        // Privacy Settings
        public bool ShowMyUniversity { get; set; } = true;
        public bool ShowMeOnApp { get; set; } = true;
        public bool ShowDistance { get; set; } = true;
        public bool ShowAge { get; set; } = true;

        // ===== İLİŞKİLER (JSON Array - UsersDto kullanarak) =====

        // Matches (Eşleşmeler)
        public List<UsersDto>? MatchesList { get; set; } = new List<UsersDto>();

        // Likes I Sent (Benim gönderdiğim like'lar)
        public List<UsersDto>? LikedUsersList { get; set; } = new List<UsersDto>();

        // Likes I Received (Bana gelen like'lar)
        public List<UsersDto>? ReceivedLikesList { get; set; } = new List<UsersDto>();

        // Passes (Sol kaydırdıklarım)
        public List<UsersDto>? PassedUsersList { get; set; } = new List<UsersDto>();

        // Blocks (Engellediğim kullanıcılar)
        public List<UsersDto>? BlockedUsersList { get; set; } = new List<UsersDto>();

        // Reports (Şikayet ettiğim kullanıcılar)
        public List<UsersDto>? ReportedUsersList { get; set; } = new List<UsersDto>();

        // Photos
        public List<Photo>? PhotosList { get; set; } = new List<Photo>();

        // JSON olarak saklanan string
        public string? PhotoImageStatusJson { get; set; }

        // Veritabanından bağımsız özellik
        [NotMapped]
        public List<string> PhotoImageStatus
        {
            get => string.IsNullOrEmpty(PhotoImageStatusJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(PhotoImageStatusJson);

            set => PhotoImageStatusJson = JsonSerializer.Serialize(value);
        }

        // Verification & Safety
        public bool IsPhotoVerified { get; set; } = false;
        public DateTime? PhotoVerifiedAt { get; set; }
        public string? FaceId { get; set; }

        // Stats & Gamification
        public int ProfileCompletionScore { get; set; } = 0;
        public int DailySwipeCount { get; set; } = 0;
        public DateTime SwipeCountResetAt { get; set; } = DateTime.UtcNow.Date;
        public int SuperLikeCount { get; set; } = 1;
        public int TotalMatchCount { get; set; } = 0;
        public int TotalLikesReceived { get; set; } = 0;

        // Premium
        public bool IsPremium { get; set; } = false;
        public DateTime? PremiumExpiresAt { get; set; }
        public int UnlimitedSwipes { get; set; } = 0;
        public int BoostsRemaining { get; set; } = 0;

        // Activity Stats
        public int MessagesSent { get; set; } = 0;
        public int MessagesReceived { get; set; } = 0;
        public double ResponseRate { get; set; } = 0;

        // Social Links
        public string? InstagramUsername { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Account Status
        public DateTime? LastActiveAt { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsProfileCompleted { get; set; } = false;


        // ============================================
        // ⭐ PREMIUM FİLTRE TERCİHLERİ (Yeni alanlar)
        // ============================================

        /// <summary>
        /// Premium kullanıcının tercih ettiği üniversite domain
        /// Örn: "bilgiedu.net" veya null (tüm üniversiteler)
        /// </summary>
        public string? PreferredUniversityDomain { get; set; }

        /// <summary>
        /// Premium kullanıcının tercih ettiği şehir
        /// </summary>
        public string? PreferredCity { get; set; }

        /// <summary>
        /// Premium kullanıcının tercih ettiği bölüm
        /// </summary>
        public string? PreferredDepartment { get; set; }


    }
}
