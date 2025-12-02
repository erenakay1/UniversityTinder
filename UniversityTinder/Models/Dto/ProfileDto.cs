namespace UniversityTinder.Models.Dto
{
    public class ProfileDto
    {
        public int ProfileId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public UserDto User { get; set; } = new();
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public int? Height { get; set; }
        public string? Department { get; set; }
        public int? YearOfStudy { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string InterestedIn { get; set; } = "Everyone";
        public int AgeRangeMin { get; set; }
        public int AgeRangeMax { get; set; }
        public int MaxDistance { get; set; }
        public bool ShowMyUniversity { get; set; }
        public List<PhotoDto>? PhotosList { get; set; }
        public List<IFormFile>? Files { get; set; }

        public List<string>? PostImageStatus { get; set; } = new List<string>();
        public int ProfileCompletionScore { get; set; }
        public bool IsPhotoVerified { get; set; }
        public bool IsPremium { get; set; }
        public int TotalMatchCount { get; set; }
    }
}
