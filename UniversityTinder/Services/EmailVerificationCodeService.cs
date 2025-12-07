using Microsoft.Extensions.Caching.Memory;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Services
{
    public class EmailVerificationCodeService : IEmailVerificationCodeService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmailVerificationCodeService> _logger;
        private const int CODE_LENGTH = 6;
        private const int CODE_EXPIRATION_MINUTES = 15;

        public EmailVerificationCodeService(IMemoryCache cache, ILogger<EmailVerificationCodeService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> GenerateVerificationCodeAsync(string email)
        {
            try
            {
                // 6 haneli rastgele kod oluştur
                var random = new Random();
                var code = random.Next(100000, 999999).ToString();

                var cacheKey = $"verification_code_{email}";
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CODE_EXPIRATION_MINUTES)
                };

                _cache.Set(cacheKey, code, cacheOptions);
                _logger.LogInformation("Email doğrulama kodu oluşturuldu: {Email}", email);

                return await Task.FromResult(code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama kodu oluşturulurken hata: {Email}", email);
                throw;
            }
        }

        public async Task<bool> ValidateVerificationCodeAsync(string email, string code)
        {
            try
            {
                var cacheKey = $"verification_code_{email}";

                if (_cache.TryGetValue(cacheKey, out string cachedCode))
                {
                    var isValid = cachedCode == code;
                    _logger.LogInformation("Email doğrulama kodu kontrolü: {Email}, Sonuç: {IsValid}", email, isValid);
                    return await Task.FromResult(isValid);
                }

                _logger.LogWarning("Email doğrulama kodu bulunamadı veya süresi dolmuş: {Email}", email);
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama kodu kontrolünde hata: {Email}", email);
                throw;
            }
        }

        public async Task InvalidateVerificationCodeAsync(string email)
        {
            try
            {
                var cacheKey = $"verification_code_{email}";
                _cache.Remove(cacheKey);
                _logger.LogInformation("Email doğrulama kodu geçersiz kılındı: {Email}", email);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama kodu geçersiz kılınırken hata: {Email}", email);
                throw;
            }
        }
    }
}