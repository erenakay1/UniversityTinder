using System.ComponentModel.DataAnnotations;

namespace UniversityTinder.Models.Dto
{
    public class SwipeRequestDto
    {
        [Required]
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Swipe tipi: "like", "pass", "superlike"
        /// </summary>
        [Required]
        [RegularExpression("^(like|pass|superlike)$")]
        public string SwipeType { get; set; } = string.Empty;
    }
}
