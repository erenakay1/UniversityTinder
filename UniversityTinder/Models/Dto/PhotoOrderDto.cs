using System.ComponentModel.DataAnnotations;

namespace UniversityTinder.Models.Dto
{
    public class PhotoOrderDto
    {
        [Required]
        public int PhotoId { get; set; }

        [Required]
        [Range(1, 6)]
        public int NewOrder { get; set; }
    }
}
