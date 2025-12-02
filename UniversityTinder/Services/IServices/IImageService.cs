using static Utility.SD;

namespace UniversityTinder.Services.IServices
{
    public interface IImageService
    {
        ImageSaveResult SaveImage(IFormFile image, string folderName, string objectId);
        Task<bool> DeleteImageAsync(string imageUrl); // YENİ
        Task<bool> DeleteImagesByPrefixAsync(string prefix); // YENİ - Toplu silme
    }
}
