using System.ComponentModel.DataAnnotations;

namespace UniversityTinder.Models.Dto
{
    public class FilterUpdateDto
    {
        [Range(18, 100)]
        public int? AgeRangeMin { get; set; }

        [Range(18, 100)]
        public int? AgeRangeMax { get; set; }

        [Range(1, 100)]
        public int? MaxDistance { get; set; }

        /// <summary>
        /// Üniversite domain filtresi (null = tüm üniversiteler)
        /// Örn: "bilgiedu.net"
        /// </summary>
        public string? UniversityDomain { get; set; }

        /// <summary>
        /// Şehir filtresi (null = tüm şehirler)
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Bölüm filtresi (null = tüm bölümler)
        /// </summary>
        public string? Department { get; set; }
    }
}
