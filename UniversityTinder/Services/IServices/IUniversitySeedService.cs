using UniversityTinder.Models.Dto;

namespace UniversityTinder.Services.IServices
{
    public interface IUniversitySeedService
    {
        Task<bool> HasDataAsync();
        Task SeedTestDataAsync(int userCount = 50);
        Task ClearAllDataAsync();
        Task<SeedStatsDto> GetSeedStatsAsync();
    }
}