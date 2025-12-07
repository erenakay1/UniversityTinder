using UniversityTinder.Models.Dto;

namespace UniversityTinder.Services.IServices
{
    public interface IImageSensorService
    {
        Task<ResponseDto?> AnalyzeImageAsync(IFormFile imageFile);
    }
}
