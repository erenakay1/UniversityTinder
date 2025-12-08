using System.ComponentModel.DataAnnotations;
using static Utility.SD;

namespace UniversityTinder.Models.Dto
{
    public class ProfileCompleteDto
    {
        // Bio (Opsiyonel ama önerilen)
        [MaxLength(500)]
        public string? Bio { get; set; }

        // Physical & Education (ZORUNLU)
        [Required(ErrorMessage = "Boy bilgisi zorunludur")]
        [Range(140, 220, ErrorMessage = "Geçerli bir boy giriniz (140-220 cm)")]
        public int Height { get; set; }

        [Required(ErrorMessage = "Bölüm bilgisi zorunludur")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sınıf bilgisi zorunludur")]
        [Range(1, 6, ErrorMessage = "Geçerli bir sınıf giriniz (1-6)")]
        public int YearOfStudy { get; set; }

        // Location (ZORUNLU - matching için kritik)
        [Required(ErrorMessage = "Konum bilgisi zorunludur")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Konum bilgisi zorunludur")]
        public double Longitude { get; set; }

        public string? City { get; set; }
        public string? District { get; set; }

        [Required(ErrorMessage = "Tercih seçimi zorunludur")]
        [EnumDataType(typeof(InterestedInType))]
        public InterestedInType InterestedIn { get; set; } = InterestedInType.Everyone;

        [Range(18, 100)]
        public int AgeRangeMin { get; set; } = 18;

        [Range(18, 100)]
        public int AgeRangeMax { get; set; } = 30;

        [Range(1, 100)]
        public int MaxDistance { get; set; } = 50; // km

        [Required(ErrorMessage = "En az 1 hobi seçmelisiniz")]
        [MinLength(1, ErrorMessage = "En az 1 hobi seçmelisiniz")]
        [MaxLength(10, ErrorMessage = "En fazla 10 hobi seçebilirsiniz")]
        public List<Hobbies> Hobbies { get; set; } = new List<Hobbies>();

        // Privacy Settings
        public bool ShowMyUniversity { get; set; } = true;
        public bool ShowMeOnApp { get; set; } = true;
        public bool ShowDistance { get; set; } = true;
        public bool ShowAge { get; set; } = true;

        // Profile Photos (EN AZ 2, MAKS 6 ZORUNLU)
        [Required(ErrorMessage = "En az 2 profil fotoğrafı yüklemelisiniz")]
        [MinLength(2, ErrorMessage = "En az 2 fotoğraf yüklemelisiniz")]
        [MaxLength(6, ErrorMessage = "En fazla 6 fotoğraf yükleyebilirsiniz")]
        public List<IFormFile> Photos { get; set; } = new List<IFormFile>();

        // Hangi fotoğraf ana profil fotoğrafı olacak (0-5 arası index)
        [Range(0, 5)]
        public int MainPhotoIndex { get; set; } = 0;
    }
}