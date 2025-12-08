using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeedController : ControllerBase
    {
        private readonly IUniversitySeedService _seedService;
        private readonly ILogger<SeedController> _logger;

        public SeedController(
            IUniversitySeedService seedService,
            ILogger<SeedController> logger)
        {
            _seedService = seedService;
            _logger = logger;
        }

        /// <summary>
        /// Veritabanının seed data içerip içermediğini kontrol eder
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetSeedStatus()
        {
            try
            {
                var hasData = await _seedService.HasDataAsync();
                var stats = hasData ? await _seedService.GetSeedStatsAsync() : null;

                return Ok(new
                {
                    hasData,
                    message = hasData ? "Database already has test data." : "Database is empty.",
                    stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking seed status");
                return StatusCode(500, new
                {
                    message = "An error occurred while checking seed status.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Detaylı seed istatistiklerini getirir
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var stats = await _seedService.GetSeedStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting seed stats");
                return StatusCode(500, new
                {
                    message = "An error occurred while getting stats.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test data oluşturur (varsayılan 50 kullanıcı)
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> SeedTestData([FromQuery] int count = 50)
        {
            try
            {
                if (count < 10 || count > 1000)
                {
                    return BadRequest(new
                    {
                        message = "User count must be between 10 and 1000."
                    });
                }

                if (await _seedService.HasDataAsync())
                {
                    return BadRequest(new
                    {
                        message = "Database already contains test data. Use /seed-force to add more or /clear to remove existing data."
                    });
                }

                await _seedService.SeedTestDataAsync(count);

                var stats = await _seedService.GetSeedStatsAsync();

                return Ok(new
                {
                    message = $"Successfully seeded {count} test users!",
                    stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding test data");
                return StatusCode(500, new
                {
                    message = "An error occurred while seeding test data.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Mevcut data'ya ek olarak yeni test data oluşturur
        /// </summary>
        [HttpPost("seed-force")]
        public async Task<IActionResult> ForceSeedTestData([FromQuery] int count = 50)
        {
            try
            {
                if (count < 10 || count > 1000)
                {
                    return BadRequest(new
                    {
                        message = "User count must be between 10 and 1000."
                    });
                }

                _logger.LogWarning("Force seeding {Count} test users - this will add to existing data!", count);

                await _seedService.SeedTestDataAsync(count);

                var stats = await _seedService.GetSeedStatsAsync();

                return Ok(new
                {
                    message = $"Successfully force seeded {count} additional test users!",
                    stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error force seeding test data");
                return StatusCode(500, new
                {
                    message = "An error occurred while force seeding test data.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Tüm seed data'yı temizler (DİKKATLİ KULLANIN!)
        /// </summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearAllData()
        {
            try
            {
                _logger.LogWarning("Clearing all seed data - THIS IS DESTRUCTIVE!");

                await _seedService.ClearAllDataAsync();

                return Ok(new
                {
                    message = "All seed data cleared successfully!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing seed data");
                return StatusCode(500, new
                {
                    message = "An error occurred while clearing seed data.",
                    error = ex.Message
                });
            }
        }
    }
}