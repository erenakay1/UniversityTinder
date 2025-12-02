using Microsoft.Extensions.Caching.Memory;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Services
{
    public class PasswordResetCodeService : IPasswordResetCodeService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<PasswordResetCodeService> _logger;

        public PasswordResetCodeService(IMemoryCache cache, ILogger<PasswordResetCodeService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> GenerateResetCodeAsync(string email)
        {
            // 6 haneli rastgele kod oluştur
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            // Cache'e 15 dakika süreyle kaydet
            var cacheKey = $"reset_code_{email}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, code, cacheOptions);

            _logger.LogInformation("Şifre sıfırlama kodu oluşturuldu: {Email}, Kod: {Code}", email, code);

            return code;
        }

        public async Task<bool> ValidateResetCodeAsync(string email, string code)
        {
            var cacheKey = $"reset_code_{email}";
            if (_cache.TryGetValue(cacheKey, out string cachedCode))
            {
                if (cachedCode == code)
                {
                    _logger.LogInformation("Şifre sıfırlama kodu doğrulandı: {Email}", email);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Yanlış kod girişi: {Email}, Girilen: {Code}", email, code);
                    return false;
                }
            }

            _logger.LogWarning("Email için kod bulunamadı: {Email}", email);
            return false;
        }


        public async Task InvalidateResetCodeAsync(string email)
        {
            var cacheKey = $"reset_code_{email}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Şifre sıfırlama kodu geçersiz kılındı: {Email}", email);
        }
    }
}
