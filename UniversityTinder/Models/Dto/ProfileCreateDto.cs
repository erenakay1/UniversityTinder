using static Utility.SD;

namespace UniversityTinder.Models.Dto
{
    public class ProfileCreateDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public int? Height { get; set; }
        public string? Department { get; set; }
        public int? YearOfStudy { get; set; }
        public InterestedInType InterestedIn { get; set; } // Man | Woman | Everyone
        public int AgeRangeMin { get; set; } = 18;
        public int AgeRangeMax { get; set; } = 30;
        public int MaxDistance { get; set; } = 50;
        public List<Hobbies>? Hobbies { get; set; } = new List<Hobbies>();
        public List<IFormFile>? Files { get; set; }

        public List<string>? PhotoImageStatus { get; set; } = new List<string>();
    }
}
