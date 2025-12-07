using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniversityTinder.Models
{
    public class Photo
    {
        [Key]
        public int PhotoId { get; set; }
        public string? PhotoImageUrl { get; set; }
        public string? PhotoImageLocalPath { get; set; }
        public string? ImageStatus { get; set; }
        public int Order { get; set; }
        public bool IsMainPhoto { get; set; }
        public bool IsVerified { get; set; } = false;
        public DateTime UploadedAt { get; set; }
        public int? ProfileId { get; set; }
        [ForeignKey("ProfileId")]
        [ValidateNever]
        [JsonIgnore]  // ⭐ JSON'da Profile property'sini gösterme
        public UserProfile? Profile { get; set; }
    }
}
