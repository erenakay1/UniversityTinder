namespace UniversityTinder.Models.Dto
{
    public class PhotoDto
    {
        public int PhotoId { get; set; }
        public string? PhotoImageUrl { get; set; }
        public string? PhotoImageLocalPath { get; set; }
        public string? ImageStatus { get; set; }
        public IFormFile? PostImage { get; set; }
        public int Order { get; set; }
        public bool IsMainPhoto { get; set; }
        public bool IsVerified { get; set; } = false;
        public DateTime UploadedAt { get; set; }
        public int? ProfileId { get; set; }
        
    }
}
