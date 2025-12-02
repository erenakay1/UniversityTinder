namespace UniversityTinder.Models.Dto
{
    public class ProfileUpdateDto
    {
        public string? Bio { get; set; }
        public int? Height { get; set; }
        public string? Department { get; set; }
        public int? YearOfStudy { get; set; }
        public string? InterestedIn { get; set; }
        public int? AgeRangeMin { get; set; }
        public int? AgeRangeMax { get; set; }
        public int? MaxDistance { get; set; }
        public bool? ShowMyUniversity { get; set; }
        public bool? ShowMeOnApp { get; set; }
        public string? InstagramUsername { get; set; }
        public List<IFormFile>? Files { get; set; }

        public List<string>? PhotoImageStatus { get; set; } = new List<string>();
    }
}
