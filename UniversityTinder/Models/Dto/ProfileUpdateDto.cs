using System.ComponentModel.DataAnnotations;

namespace UniversityTinder.Models.Dto
{
    public class ProfileUpdateDto
    {
        // Bio
        [MaxLength(500)]
        public string? Bio { get; set; }

        // Physical & Education
        [Range(140, 220, ErrorMessage = "Geçerli bir boy giriniz (140-220 cm)")]
        public int? Height { get; set; }

        public string? Department { get; set; }

        [Range(1, 6, ErrorMessage = "Geçerli bir sınıf giriniz (1-6)")]
        public int? YearOfStudy { get; set; }

        // Location (Kullanıcı konum güncelleyebilir)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }

        // Dating Preferences
        public string? InterestedIn { get; set; } // "Men", "Women", "Everyone"

        [Range(18, 100)]
        public int? AgeRangeMin { get; set; }

        [Range(18, 100)]
        public int? AgeRangeMax { get; set; }

        [Range(1, 100)]
        public int? MaxDistance { get; set; } // km

        // Privacy Settings
        public bool? ShowMyUniversity { get; set; }
        public bool? ShowMeOnApp { get; set; }
        public bool? ShowDistance { get; set; }
        public bool? ShowAge { get; set; }

        // Social Links
        public string? InstagramUsername { get; set; }
    }
}