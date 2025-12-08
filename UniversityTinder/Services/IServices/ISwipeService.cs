using UniversityTinder.Models.Dto;

namespace UniversityTinder.Services.IServices
{
    public interface ISwipeService
    {
        /// <summary>
        /// Kullanıcı için potansiyel eşleşmeleri getirir (Hybrid algoritma ile)
        /// </summary>
        Task<List<ProfileCardDto>> GetPotentialMatches(string userId);

        /// <summary>
        /// Profili beğenir (sağa kaydırma)
        /// </summary>
        Task<SwipeResultDto> Like(string userId, string targetUserId);

        /// <summary>
        /// Profili geçer (sola kaydırma)
        /// </summary>
        Task<SwipeResultDto> Pass(string userId, string targetUserId);

        /// <summary>
        /// Super like (yukarı kaydırma)
        /// </summary>
        Task<SwipeResultDto> SuperLike(string userId, string targetUserId);

        /// <summary>
        /// Son swipe'ı geri alır (Premium only)
        /// </summary>
        Task<SwipeResultDto> UndoLastSwipe(string userId);

        /// <summary>
        /// Kullanıcının swipe istatistiklerini getirir
        /// </summary>
        Task<SwipeStatsDto> GetSwipeStats(string userId);

        /// <summary>
        /// Premium kullanıcının filtrelerini günceller
        /// </summary>
        Task<ResponseDto> UpdateFilters(string userId, FilterUpdateDto filterDto);

        /// <summary>
        /// Günlük swipe limitini kontrol eder
        /// </summary>
        Task<bool> CheckDailySwipeLimit(string userId);

        /// <summary>
        /// Super like limitini kontrol eder
        /// </summary>
        Task<bool> CheckSuperLikeLimit(string userId);
    }
}
