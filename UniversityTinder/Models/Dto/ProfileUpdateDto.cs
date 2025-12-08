using System.ComponentModel.DataAnnotations;
using static Utility.SD;

namespace UniversityTinder.Models.Dto
{
    public class ProfileUpdateDto
    {
        // ============================================
        // TEMEL BİLGİLER
        // ============================================

        [StringLength(500)]
        public string? Bio { get; set; }

        [Range(140, 220)]
        public int? Height { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [Range(1, 8)]
        public int? YearOfStudy { get; set; }

        // ============================================
        // LOKASYON BİLGİLERİ
        // ============================================

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        // ============================================
        // DATING PREFERENCES
        // ============================================

        [EnumDataType(typeof(InterestedInType), ErrorMessage = "Geçersiz tercih türü")]
        public InterestedInType? InterestedIn { get; set; }

        [Range(18, 100)]
        public int? AgeRangeMin { get; set; }

        [Range(18, 100)]
        public int? AgeRangeMax { get; set; }

        [Range(1, 100)]
        public int? MaxDistance { get; set; }

        [MaxLength(10, ErrorMessage = "En fazla 10 hobi seçebilirsiniz")]
        public List<Hobbies>? Hobbies { get; set; }

        // ============================================
        // PRIVACY SETTINGS
        // ============================================

        public bool? ShowMyUniversity { get; set; }
        public bool? ShowMeOnApp { get; set; }
        public bool? ShowDistance { get; set; }
        public bool? ShowAge { get; set; }

        // ============================================
        // SOCIAL LINKS
        // ============================================

        [StringLength(30)]
        public string? InstagramUsername { get; set; }

        // ============================================
        // FOTOĞRAF YÖNETİMİ
        // ============================================

        /// <summary>
        /// Eklenecek yeni fotoğraflar (Maksimum 6 fotoğraf kontrolü yapılır)
        /// </summary>
        public List<IFormFile>? NewPhotos { get; set; }

        /// <summary>
        /// Silinecek fotoğrafların ID'leri
        /// </summary>
        public List<int>? PhotoIdsToDelete { get; set; }

        /// <summary>
        /// Yeni ana fotoğraf ID'si (İsteğe bağlı)
        /// </summary>
        public int? NewMainPhotoId { get; set; }

        /// <summary>
        /// Fotoğraf sıralaması (İsteğe bağlı)
        /// </summary>
        public List<PhotoOrderDto>? PhotoOrders { get; set; }
    }
}