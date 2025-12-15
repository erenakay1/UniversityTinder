using AutoMapper;
using UniversityTinder.Data;
using UniversityTinder.Models.Dto;
using UniversityTinder.Models;
using UniversityTinder.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using static Utility.SD;

namespace UniversityTinder.Services
{
    public class SwipeService : ISwipeService
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;
        private readonly ILogger<SwipeService> _logger;

        // Constants
        private const int FREE_DAILY_SWIPE_LIMIT = 30;
        private const int FREE_DAILY_SUPERLIKE_LIMIT = 1;
        private const int PREMIUM_DAILY_SUPERLIKE_LIMIT = 5;
        private const int FREE_MAX_DISTANCE = 50; // km
        private const int FREE_AGE_MIN = 18;
        private const int FREE_AGE_MAX = 30;

        public SwipeService(
            AppDbContext db,
            IMapper mapper,
            ILogger<SwipeService> logger)
        {
            _db = db;
            _mapper = mapper;
            _logger = logger;
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// İki profil arasındaki mesafeyi hesaplar (Haversine formula)
        /// </summary>
        private double CalculateDistance(UserProfile profile1, UserProfile profile2)
        {
            if (!profile1.Latitude.HasValue || !profile1.Longitude.HasValue ||
                !profile2.Latitude.HasValue || !profile2.Longitude.HasValue)
            {
                return double.MaxValue; // Konum bilgisi yoksa çok uzak say
            }

            var lat1 = profile1.Latitude.Value;
            var lon1 = profile1.Longitude.Value;
            var lat2 = profile2.Latitude.Value;
            var lon2 = profile2.Longitude.Value;

            const double R = 6371; // Dünya yarıçapı (km)

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = R * c;

            return distance;
        }

        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        /// <summary>
        /// Yaş hesaplama
        /// </summary>
        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        /// <summary>
        /// Cinsiyet uyumluluğu kontrolü (karşılıklı) - ENUM VERSION
        /// </summary>
        private bool IsGenderCompatible(UserProfile currentUser, UserProfile targetUser)
        {
            var currentUserGender = currentUser.User.Gender; // GenderType enum
            var targetUserGender = targetUser.User.Gender;   // GenderType enum

            var currentUserInterestedIn = currentUser.InterestedIn; // InterestedInType enum
            var targetUserInterestedIn = targetUser.InterestedIn;   // InterestedInType enum

            // Current user'ın tercihi
            bool currentUserInterested = currentUserInterestedIn == InterestedInType.Everyone ||
                                        (currentUserInterestedIn == InterestedInType.Men && targetUserGender == GenderType.Male) ||
                                        (currentUserInterestedIn == InterestedInType.Women && targetUserGender == GenderType.Female);

            // Target user'ın tercihi
            bool targetUserInterested = targetUserInterestedIn == InterestedInType.Everyone ||
                                       (targetUserInterestedIn == InterestedInType.Men && currentUserGender == GenderType.Male) ||
                                       (targetUserInterestedIn == InterestedInType.Women && currentUserGender == GenderType.Female);

            // Her iki taraf da uyumlu olmalı
            return currentUserInterested && targetUserInterested;
        }

        /// <summary>
        /// Liste shuffle (Fisher-Yates algoritması)
        /// </summary>
        private List<T> Shuffle<T>(IEnumerable<T> list)
        {
            var array = list.ToArray();
            var rng = new Random();
            int n = array.Length;

            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (array[k], array[n]) = (array[n], array[k]);
            }

            return array.ToList();
        }

        /// <summary>
        /// UserProfile'ı ProfileCardDto'ya map eder
        /// </summary>
        private ProfileCardDto MapToProfileCardDto(UserProfile profile, UserProfile currentUser)
        {
            return new ProfileCardDto
            {
                ProfileId = profile.ProfileId,
                UserId = profile.UserId,
                DisplayName = profile.DisplayName,
                Age = CalculateAge(profile.User.DateOfBirth),
                Bio = profile.Bio,
                Photos = profile.PhotosList?
                    .OrderBy(p => p.Order)
                    .Select(p => p.PhotoImageUrl ?? "")
                    .Where(url => !string.IsNullOrEmpty(url))
                    .ToList() ?? new List<string>(),
                UniversityName = profile.User.UniversityName,
                Department = profile.Department,
                YearOfStudy = profile.YearOfStudy,
                Distance = (int)Math.Round(CalculateDistance(currentUser, profile)),
                IsVerified = profile.IsPhotoVerified,
                IsPremium = profile.IsPremium,
                ShowUniversity = currentUser.IsPremium || profile.ShowMyUniversity,

                // Beni like'ladı mı? (Sadece premium görebilir)
                HasLikedMe = currentUser.IsPremium &&
                            profile.LikedUsersList.Any(u => u.UserId == currentUser.UserId),

                LikedMeAt = currentUser.IsPremium
                    ? profile.LikedUsersList
                        .Where(u => u.UserId == currentUser.UserId)
                        .Select(u => (DateTime?)DateTime.UtcNow) // TODO: Gerçek timestamp eklenecek
                        .FirstOrDefault()
                    : null
            };
        }

        /// <summary>
        /// Günlük swipe sayacını sıfırlar (gerekirse)
        /// </summary>
        private async Task ResetDailySwipeCountIfNeeded(UserProfile profile)
        {
            var today = DateTime.UtcNow.Date;
            if (profile.SwipeCountResetAt.Date < today)
            {
                _logger.LogInformation("Günlük swipe sayacı sıfırlanıyor. UserId: {UserId}", profile.UserId);
                profile.DailySwipeCount = 0;
                profile.SwipeCountResetAt = today;
                await _db.SaveChangesAsync();
            }
        }



        /// <summary>
        /// Kullanıcı için potansiyel eşleşmeleri getirir (Hybrid algoritma ile)
        /// </summary>
        public async Task<List<ProfileCardDto>> GetPotentialMatches(string userId)
        {
            try
            {
                _logger.LogInformation("GetPotentialMatches başlatıldı. UserId: {UserId}", userId);

                // ============================================
                // CURRENT USER PROFILE
                // ============================================
                var currentUser = await _db.UserProfiles
                    .Include(p => p.User)
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.PassedUsersList)
                    //.Include(p => p.MatchesList)
                    //.Include(p => p.BlockedUsersList)
                    .Include(p => p.PhotosList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (currentUser == null)
                {
                    _logger.LogWarning("Profil bulunamadı. UserId: {UserId}", userId);
                    throw new Exception("Profil bulunamadı");
                }

                // Günlük sayaç sıfırlama kontrolü
                await ResetDailySwipeCountIfNeeded(currentUser);

                // Referans tarih (Bugün)
                var today = DateTime.UtcNow.Date;
                // ============================================
                // BASE FİLTRELEME (Herkes için geçerli)
                // ============================================
                var query = _db.UserProfiles
                    .Include(p => p.User)
                    .Include(p => p.PhotosList)
                    //.Include(p => p.LikedUsersList)
                    .Where(p =>
                        p.UserId != userId &&                    // Kendim değil
                        p.IsProfileCompleted &&                  // Profil tamamlanmış
                        p.IsActive &&                            // Aktif
                        p.ShowMeOnApp                           // Görünür
                    );

                // ============================================
                // ⭐ PREMIUM vs FREE FİLTRELEME
                // ============================================
                if (currentUser.IsPremium)
                {
                    // ✅ PREMIUM: Kullanıcının tercihlerine göre filtrele
                    _logger.LogInformation("Premium user - özel filtreler uygulanıyor");

                    // Yaş filtresi (premium kendi aralığını belirlemiş)
                    //query = query.Where(p =>
                    //    CalculateAge(p.User.DateOfBirth) >= currentUser.AgeRangeMin &&
                    //    CalculateAge(p.User.DateOfBirth) <= currentUser.AgeRangeMax
                    //);

                    var minDateOfBirth = today.AddYears(-(currentUser.AgeRangeMax + 1));
                    var maxDateOfBirth = today.AddYears(-currentUser.AgeRangeMin);

                    query = query.Where(p =>
                        p.User.DateOfBirth > minDateOfBirth &&
                        p.User.DateOfBirth <= maxDateOfBirth
                    );

                    // ⭐ Üniversite filtresi (varsa)
                    if (!string.IsNullOrEmpty(currentUser.PreferredUniversityDomain))
                    {
                        query = query.Where(p =>
                            p.User.UniversityDomain == currentUser.PreferredUniversityDomain
                        );
                        _logger.LogDebug("Üniversite filtresi: {Domain}", currentUser.PreferredUniversityDomain);
                    }

                    // ⭐ Şehir filtresi (varsa)
                    if (!string.IsNullOrEmpty(currentUser.PreferredCity))
                    {
                        query = query.Where(p => p.City == currentUser.PreferredCity);
                        _logger.LogDebug("Şehir filtresi: {City}", currentUser.PreferredCity);
                    }

                    // ⭐ Bölüm filtresi (varsa)
                    if (!string.IsNullOrEmpty(currentUser.PreferredDepartment))
                    {
                        query = query.Where(p => p.Department == currentUser.PreferredDepartment);
                        _logger.LogDebug("Bölüm filtresi: {Department}", currentUser.PreferredDepartment);
                    }
                }
                else
                {
                    // ❌ FREE: Sabit filtreler (kullanıcı değiştiremez)
                    _logger.LogInformation("Free user - varsayılan filtreler uygulanıyor");

                    // Sabit yaş aralığı: 18-30
                    //query = query.Where(p =>
                    //    CalculateAge(p.User.DateOfBirth) >= FREE_AGE_MIN &&
                    //    CalculateAge(p.User.DateOfBirth) <= FREE_AGE_MAX
                    //);
                    var minDateOfBirth = today.AddYears(-(FREE_AGE_MAX + 1));
                    var maxDateOfBirth = today.AddYears(-FREE_AGE_MIN);

                    query = query.Where(p =>
                        p.User.DateOfBirth > minDateOfBirth &&
                        p.User.DateOfBirth <= maxDateOfBirth
                    );

                    // ÜNİVERSİTE FİLTRESİ YOK (rastgele gelecek)
                    _logger.LogDebug("Free user - tüm üniversitelerden gösteriliyor");
                }

                // Cinsiyet filtrelemesi (herkes için)
                var allCandidates = await query.ToListAsync();
                allCandidates = allCandidates
                    .Where(p => IsGenderCompatible(currentUser, p))
                    .ToList();

                _logger.LogDebug("Cinsiyet filtresi sonrası: {Count} aday", allCandidates.Count);

                // ============================================
                // DİĞER FİLTRELEMELER
                // ============================================

                // Daha önce interaction olmamış
                allCandidates = allCandidates
                    .Where(p =>
                        !currentUser.LikedUsersList.Any(u => u.UserId == p.UserId) &&
                        !currentUser.PassedUsersList.Any(u => u.UserId == p.UserId) &&
                        !currentUser.MatchesList.Any(u => u.UserId == p.UserId) &&
                        !currentUser.BlockedUsersList.Any(u => u.UserId == p.UserId)
                    )
                    .ToList();

                _logger.LogDebug("Interaction filtresi sonrası: {Count} aday", allCandidates.Count);

                // Mesafe filtrelemesi
                if (currentUser.IsPremium)
                {
                    // Premium: Kendi belirlediği mesafe
                    allCandidates = allCandidates
                        .Where(p => CalculateDistance(currentUser, p) <= currentUser.MaxDistance)
                        .ToList();
                }
                else
                {
                    // Free: Sabit 50km
                    allCandidates = allCandidates
                        .Where(p => CalculateDistance(currentUser, p) <= FREE_MAX_DISTANCE)
                        .ToList();
                }

                _logger.LogDebug("Mesafe filtresi sonrası: {Count} aday", allCandidates.Count);

                // ============================================
                // KATEGORİLERE AYIRMA (Hybrid Model)
                // ============================================
                var whoLikedMe = allCandidates
                    .Where(p => p.LikedUsersList.Any(u => u.UserId == userId))
                    .ToList();

                var premiumUsers = allCandidates
                    .Except(whoLikedMe)
                    .Where(p => p.IsPremium)
                    .ToList();

                var verifiedUsers = allCandidates
                    .Except(whoLikedMe)
                    .Except(premiumUsers)
                    .Where(p => p.IsPhotoVerified)
                    .ToList();

                var regularUsers = allCandidates
                    .Except(whoLikedMe)
                    .Except(premiumUsers)
                    .Except(verifiedUsers)
                    .ToList();

                _logger.LogInformation(
                    "Kategoriler: WhoLikedMe={Liked}, Premium={Premium}, Verified={Verified}, Regular={Regular}",
                    whoLikedMe.Count, premiumUsers.Count, verifiedUsers.Count, regularUsers.Count);

                // ============================================
                // HYBRID MODEL DAĞILIMI
                // ============================================
                var result = new List<UserProfile>();

                // İLK BATCH: 15 kişi hedef
                var batch1Liked = Shuffle(whoLikedMe).Take(10).ToList();
                result.AddRange(batch1Liked);

                var batch1Prem = Shuffle(premiumUsers).Take(3).ToList();
                result.AddRange(batch1Prem);

                var batch1Verif = Shuffle(verifiedUsers).Take(2).ToList();
                result.AddRange(batch1Verif);

                int batch1Needed = 15 - result.Count;
                if (batch1Needed > 0)
                {
                    result.AddRange(Shuffle(regularUsers).Take(batch1Needed));
                }

                // İKİNCİ BATCH: 20 kişi hedef (toplam 35)
                var batch2Liked = Shuffle(whoLikedMe).Except(batch1Liked).Take(8).ToList();
                result.AddRange(batch2Liked);

                var batch2Prem = Shuffle(premiumUsers).Except(batch1Prem).Take(6).ToList();
                result.AddRange(batch2Prem);

                var batch2Verif = Shuffle(verifiedUsers).Except(batch1Verif).Take(6).ToList();
                result.AddRange(batch2Verif);

                int batch2Needed = 35 - result.Count;
                if (batch2Needed > 0)
                {
                    var usedRegular = result.Where(r => regularUsers.Contains(r)).ToList();
                    result.AddRange(Shuffle(regularUsers).Except(usedRegular).Take(batch2Needed));
                }

                // ÜÇÜNCÜ BATCH: 15 kişi hedef (toplam 50)
                int batch3Needed = 50 - result.Count;
                if (batch3Needed > 0)
                {
                    var remaining = allCandidates.Except(result).ToList();
                    result.AddRange(Shuffle(remaining).Take(batch3Needed));
                }

                // ============================================
                // FALLBACK
                // ============================================
                if (result.Count == 0)
                {
                    _logger.LogWarning(
                        "Potansiyel eşleşme bulunamadı. UserId: {UserId}, IsPremium: {IsPremium}",
                        userId, currentUser.IsPremium);

                    return new List<ProfileCardDto>();
                }

                _logger.LogInformation("Toplam {Count} profil hazırlandı", result.Count);

                // ============================================
                // FINAL SHUFFLE & DTO MAPPING
                // ============================================
                var shuffled = Shuffle(result);

                return shuffled.Select(p => MapToProfileCardDto(p, currentUser)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPotentialMatches hatası. UserId: {UserId}", userId);
                throw;
            }
        }


        /// <summary>
        /// Profili beğenir (sağa kaydırma)
        /// </summary>
        public async Task<SwipeResultDto> Like(string userId, string targetUserId)
        {
            try
            {
                _logger.LogInformation("Like işlemi başlatıldı. From: {UserId}, To: {TargetUserId}", userId, targetUserId);

                // ============================================
                // USER PROFILES LOAD
                // ============================================
                var currentUser = await _db.UserProfiles
                    .Include(p => p.User)
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.ReceivedLikesList)
                    //.Include(p => p.MatchesList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                var targetUser = await _db.UserProfiles
                    .Include(p => p.User)
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.ReceivedLikesList)
                    //.Include(p => p.MatchesList)
                    .FirstOrDefaultAsync(p => p.UserId == targetUserId);

                if (currentUser == null || targetUser == null)
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Kullanıcı bulunamadı"
                    };
                }

                // ============================================
                // DAILY LIMIT CHECK (Free users only)
                // ============================================
                await ResetDailySwipeCountIfNeeded(currentUser);

                if (!currentUser.IsPremium)
                {
                    if (currentUser.DailySwipeCount >= FREE_DAILY_SWIPE_LIMIT)
                    {
                        _logger.LogWarning("Günlük swipe limiti aşıldı. UserId: {UserId}", userId);

                        return new SwipeResultDto
                        {
                            IsSuccess = false,
                            Message = "Günlük swipe limitine ulaştınız! 😔",
                            ShowPaywall = true,
                            PaywallType = "SWIPE_LIMIT",
                            PaywallMessage = "Premium üyelikle sınırsız swipe yapabilirsin! 🚀\n\n" +
                                            "✨ Tüm üniversitelerden profil gör\n" +
                                            "✨ Kimin beğendiğini gör\n" +
                                            "✨ Sınırsız swipe\n" +
                                            "✨ 5 super like/gün",
                            RemainingSwipes = 0
                        };
                    }
                }

                // ============================================
                // LIKE İŞLEMİ
                // ============================================

                // Zaten like atmış mıyım kontrolü
                if (currentUser.LikedUsersList.Any(u => u.UserId == targetUserId))
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Bu kullanıcıyı zaten beğendiniz"
                    };
                }

                // UsersDto oluştur ve like listesine ekle
                var targetUserDto = new UsersDto
                {
                    Id = targetUserId,
                    UserId = targetUserId,
                    Name = targetUser.User.FirstName,
                    Surname = targetUser.User.LastName,
                    DisplayName = targetUser.DisplayName,
                    Gender = targetUser.User.Gender,
                    Email = targetUser.User.Email,
                    ProfileImageUrl = targetUser.ProfileImageUrl,
                    Age = CalculateAge(targetUser.User.DateOfBirth),
                    UniversityName = targetUser.User.UniversityName,
                    IsVerified = targetUser.IsPhotoVerified
                };

                currentUser.LikedUsersList.Add(targetUserDto);

                // Günlük swipe sayacını artır
                currentUser.DailySwipeCount++;

                // ============================================
                // MATCH DETECTION
                // ============================================
                bool isMatch = targetUser.LikedUsersList.Any(u => u.UserId == userId);

                UserDto? matchedUser = null;

                if (isMatch)
                {
                    _logger.LogInformation("🎉 MATCH! User1: {User1}, User2: {User2}", userId, targetUserId);

                    // Her iki tarafa da match ekle
                    var currentUserDto = new UsersDto
                    {
                        Id = userId,
                        UserId = userId,
                        Name = currentUser.User.FirstName,
                        Surname = currentUser.User.LastName,
                        DisplayName = currentUser.DisplayName,
                        Gender = currentUser.User.Gender,
                        Email = currentUser.User.Email,
                        ProfileImageUrl = currentUser.ProfileImageUrl,
                        Age = CalculateAge(currentUser.User.DateOfBirth),
                        UniversityName = currentUser.User.UniversityName,
                        IsVerified = currentUser.IsPhotoVerified
                    };

                    currentUser.MatchesList.Add(targetUserDto);
                    targetUser.MatchesList.Add(currentUserDto);

                    // Match count güncelle
                    currentUser.TotalMatchCount++;
                    targetUser.TotalMatchCount++;

                    // DTO için kullanıcı bilgisi
                    matchedUser = new UserDto
                    {
                        Id = targetUserId,
                        UserId = targetUserId,
                        Name = targetUser.User.FirstName,
                        Surname = targetUser.User.LastName,
                        DisplayName = targetUser.DisplayName,
                        Gender = targetUser.User.Gender,
                        ProfileImageUrl = targetUser.ProfileImageUrl,
                        Age = CalculateAge(targetUser.User.DateOfBirth),
                        UniversityName = targetUser.User.UniversityName,
                        IsVerified = targetUser.IsPhotoVerified
                    };

                    // TODO: Match notification gönder
                }

                // ============================================
                // LIKE NOTIFICATION (Karşı tarafa)
                // ============================================
                // TODO: SendLikeNotification(userId, targetUserId);

                // ============================================
                // SAVE CHANGES
                // ============================================
                await _db.SaveChangesAsync();

                var remainingSwipes = currentUser.IsPremium
                    ? -1  // Unlimited
                    : FREE_DAILY_SWIPE_LIMIT - currentUser.DailySwipeCount;

                return new SwipeResultDto
                {
                    IsSuccess = true,
                    Message = isMatch ? "🎉 Match! Artık mesajlaşabilirsiniz!" : "Beğeni gönderildi",
                    IsMatch = isMatch,
                    MatchedUser = matchedUser,
                    RemainingSwipes = remainingSwipes,
                    RemainingSuperLikes = currentUser.SuperLikeCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Like işlemi hatası. UserId: {UserId}, TargetUserId: {TargetUserId}",
                    userId, targetUserId);
                throw;
            }
        }

        /// <summary>
        /// Profili geçer (sola kaydırma)
        /// </summary>
        public async Task<SwipeResultDto> Pass(string userId, string targetUserId)
        {
            try
            {
                _logger.LogInformation("Pass işlemi başlatıldı. From: {UserId}, To: {TargetUserId}", userId, targetUserId);

                var currentUser = await _db.UserProfiles
                    .Include(p => p.User)
                    //.Include(p => p.PassedUsersList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                var targetUser = await _db.UserProfiles
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.UserId == targetUserId);

                if (currentUser == null || targetUser == null)
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Kullanıcı bulunamadı"
                    };
                }

                // Daily limit check
                await ResetDailySwipeCountIfNeeded(currentUser);

                if (!currentUser.IsPremium)
                {
                    if (currentUser.DailySwipeCount >= FREE_DAILY_SWIPE_LIMIT)
                    {
                        return new SwipeResultDto
                        {
                            IsSuccess = false,
                            Message = "Günlük swipe limitine ulaştınız!",
                            ShowPaywall = true,
                            PaywallType = "SWIPE_LIMIT",
                            RemainingSwipes = 0
                        };
                    }
                }

                // Zaten pass yapmış mıyım kontrolü
                if (currentUser.PassedUsersList.Any(u => u.UserId == targetUserId))
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Bu kullanıcıyı zaten geçtiniz"
                    };
                }

                // Pass listesine ekle
                var targetUserDto = new UsersDto
                {
                    Id = targetUserId,
                    UserId = targetUserId,
                    Name = targetUser.User.FirstName,
                    Surname = targetUser.User.LastName,
                    DisplayName = targetUser.DisplayName,
                    IsPassed = true
                };

                currentUser.PassedUsersList.Add(targetUserDto);
                currentUser.DailySwipeCount++;

                await _db.SaveChangesAsync();

                var remainingSwipes = currentUser.IsPremium
                    ? -1
                    : FREE_DAILY_SWIPE_LIMIT - currentUser.DailySwipeCount;

                return new SwipeResultDto
                {
                    IsSuccess = true,
                    Message = "Geçildi",
                    IsMatch = false,
                    RemainingSwipes = remainingSwipes,
                    RemainingSuperLikes = currentUser.SuperLikeCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pass işlemi hatası");
                throw;
            }
        }

        /// <summary>
        /// Super like (yukarı kaydırma)
        /// </summary>
        public async Task<SwipeResultDto> SuperLike(string userId, string targetUserId)
        {
            try
            {
                _logger.LogInformation("SuperLike işlemi başlatıldı. From: {UserId}, To: {TargetUserId}",
                    userId, targetUserId);

                var currentUser = await _db.UserProfiles
                    .Include(p => p.User)
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.ReceivedLikesList)
                    //.Include(p => p.MatchesList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                var targetUser = await _db.UserProfiles
                    .Include(p => p.User)
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.ReceivedLikesList)
                    //.Include(p => p.MatchesList)
                    .FirstOrDefaultAsync(p => p.UserId == targetUserId);

                if (currentUser == null || targetUser == null)
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Kullanıcı bulunamadı"
                    };
                }

                // ============================================
                // SUPER LIKE LIMIT CHECK
                // ============================================
                if (currentUser.SuperLikeCount <= 0)
                {
                    _logger.LogWarning("Super like limiti aşıldı. UserId: {UserId}", userId);

                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Günlük super like limitiniz doldu! 💫",
                        ShowPaywall = true,
                        PaywallType = "SUPER_LIKE_LIMIT",
                        PaywallMessage = "Premium üyelikle günde 5 super like hakkın olur! ⭐",
                        RemainingSuperLikes = 0
                    };
                }

                // Zaten like atmış mıyım kontrolü
                if (currentUser.LikedUsersList.Any(u => u.UserId == targetUserId))
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Bu kullanıcıyı zaten beğendiniz"
                    };
                }

                // Like listesine ekle (super like flag ile)
                var targetUserDto = new UsersDto
                {
                    Id = targetUserId,
                    UserId = targetUserId,
                    Name = targetUser.User.FirstName,
                    Surname = targetUser.User.LastName,
                    DisplayName = targetUser.DisplayName,
                    Gender = targetUser.User.Gender,
                    ProfileImageUrl = targetUser.ProfileImageUrl,
                    IsSuperLike = true  // ⭐ Super like flag
                };

                currentUser.LikedUsersList.Add(targetUserDto);
                currentUser.SuperLikeCount--;  // Super like hakkını azalt

                // Match kontrolü (Like ile aynı)
                bool isMatch = targetUser.LikedUsersList.Any(u => u.UserId == userId);
                UserDto? matchedUser = null;

                if (isMatch)
                {
                    _logger.LogInformation("🎉 SUPER MATCH! User1: {User1}, User2: {User2}", userId, targetUserId);

                    var currentUserDto = new UsersDto
                    {
                        Id = userId,
                        UserId = userId,
                        Name = currentUser.User.FirstName,
                        Surname = currentUser.User.LastName,
                        DisplayName = currentUser.DisplayName,
                        ProfileImageUrl = currentUser.ProfileImageUrl
                    };

                    currentUser.MatchesList.Add(targetUserDto);
                    targetUser.MatchesList.Add(currentUserDto);

                    currentUser.TotalMatchCount++;
                    targetUser.TotalMatchCount++;

                    matchedUser = new UserDto
                    {
                        Id = targetUserId,
                        UserId = targetUserId,
                        Name = targetUser.User.FirstName,
                        Surname = targetUser.User.LastName,
                        DisplayName = targetUser.DisplayName,
                        ProfileImageUrl = targetUser.ProfileImageUrl
                    };
                }

                // TODO: Super like notification gönder (özel görünür)

                await _db.SaveChangesAsync();

                return new SwipeResultDto
                {
                    IsSuccess = true,
                    Message = isMatch ? "🌟 Super Match!" : "⭐ Super Like gönderildi!",
                    IsMatch = isMatch,
                    MatchedUser = matchedUser,
                    RemainingSuperLikes = currentUser.SuperLikeCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SuperLike işlemi hatası");
                throw;
            }
        }


        /// <summary>
        /// Son swipe'ı geri alır (Premium only)
        /// </summary>
        public async Task<SwipeResultDto> UndoLastSwipe(string userId)
        {
            try
            {
                _logger.LogInformation("UndoLastSwipe başlatıldı. UserId: {UserId}", userId);

                var currentUser = await _db.UserProfiles
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.PassedUsersList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (currentUser == null)
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Kullanıcı bulunamadı"
                    };
                }

                // Premium kontrolü
                if (!currentUser.IsPremium)
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Geri alma özelliği sadece Premium üyeler için!",
                        ShowPaywall = true,
                        PaywallType = "UNDO_FEATURE",
                        PaywallMessage = "Premium ol, son swipe'ını geri al! ⏪"
                    };
                }

                // Son swipe'ı bul (liked veya passed)
                var lastLiked = currentUser.LikedUsersList.OrderByDescending(u => u.Id).FirstOrDefault();
                var lastPassed = currentUser.PassedUsersList.OrderByDescending(u => u.Id).FirstOrDefault();

                // TODO: Timestamp ile hangisi daha yeni belirlenmeli
                // Şimdilik basit olarak liked'a öncelik veriyoruz

                if (lastLiked != null)
                {
                    currentUser.LikedUsersList.Remove(lastLiked);
                    currentUser.DailySwipeCount = Math.Max(0, currentUser.DailySwipeCount - 1);

                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Son like geri alındı. UserId: {UserId}, TargetUserId: {TargetUserId}",
                        userId, lastLiked.UserId);

                    return new SwipeResultDto
                    {
                        IsSuccess = true,
                        Message = "Son beğeni geri alındı ⏪"
                    };
                }
                else if (lastPassed != null)
                {
                    currentUser.PassedUsersList.Remove(lastPassed);
                    currentUser.DailySwipeCount = Math.Max(0, currentUser.DailySwipeCount - 1);

                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Son pass geri alındı. UserId: {UserId}, TargetUserId: {TargetUserId}",
                        userId, lastPassed.UserId);

                    return new SwipeResultDto
                    {
                        IsSuccess = true,
                        Message = "Son geçme geri alındı ⏪"
                    };
                }
                else
                {
                    return new SwipeResultDto
                    {
                        IsSuccess = false,
                        Message = "Geri alınacak swipe bulunamadı"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UndoLastSwipe hatası. UserId: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Kullanıcının swipe istatistiklerini getirir
        /// </summary>
        public async Task<SwipeStatsDto> GetSwipeStats(string userId)
        {
            try
            {
                var profile = await _db.UserProfiles
                    //.Include(p => p.LikedUsersList)
                    //.Include(p => p.PassedUsersList)
                    //.Include(p => p.MatchesList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    throw new Exception("Profil bulunamadı");
                }

                await ResetDailySwipeCountIfNeeded(profile);

                var remainingSwipes = profile.IsPremium
                    ? -1
                    : FREE_DAILY_SWIPE_LIMIT - profile.DailySwipeCount;

                // Bugünkü aktivite (basit versiyon - detaylı için timestamp gerekli)
                var today = DateTime.UtcNow.Date;

                return new SwipeStatsDto
                {
                    TotalSwipesToday = profile.DailySwipeCount,
                    RemainingSwipes = remainingSwipes,
                    SuperLikesRemaining = profile.SuperLikeCount,
                    SwipeCountResetAt = profile.SwipeCountResetAt,
                    IsPremium = profile.IsPremium,

                    // Bugünkü aktivite (yaklaşık)
                    LikesToday = Math.Min(profile.DailySwipeCount, profile.LikedUsersList.Count),
                    PassesToday = Math.Min(profile.DailySwipeCount, profile.PassedUsersList.Count),
                    SuperLikesToday = 0, // TODO: Timestamp ile hesaplanabilir
                    MatchesToday = 0 // TODO: Match timestamp eklenmeli
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSwipeStats hatası. UserId: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Premium kullanıcının filtrelerini günceller
        /// </summary>
        public async Task<ResponseDto> UpdateFilters(string userId, FilterUpdateDto filterDto)
        {
            try
            {
                var profile = await _db.UserProfiles
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    return new ResponseDto
                    {
                        IsSuccess = false,
                        Message = "Profil bulunamadı",
                        StatusCode = HttpStatusCode.NotFound
                    };
                }

                // Premium kontrolü
                if (!profile.IsPremium)
                {
                    return new ResponseDto
                    {
                        IsSuccess = false,
                        Message = "Bu özellik sadece Premium üyeler için!",
                        StatusCode = HttpStatusCode.Forbidden
                    };
                }

                // Filtreleri güncelle
                if (filterDto.AgeRangeMin.HasValue)
                    profile.AgeRangeMin = filterDto.AgeRangeMin.Value;

                if (filterDto.AgeRangeMax.HasValue)
                    profile.AgeRangeMax = filterDto.AgeRangeMax.Value;

                if (filterDto.MaxDistance.HasValue)
                    profile.MaxDistance = filterDto.MaxDistance.Value;

                // Üniversite filtresi (null = tüm üniversiteler)
                profile.PreferredUniversityDomain = filterDto.UniversityDomain;

                // Şehir filtresi
                profile.PreferredCity = filterDto.City;

                // Bölüm filtresi
                profile.PreferredDepartment = filterDto.Department;

                profile.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Premium filtreler güncellendi. UserId: {UserId}", userId);

                return new ResponseDto
                {
                    IsSuccess = true,
                    Message = "Filtreler güncellendi",
                    StatusCode = HttpStatusCode.OK,
                    Result = new
                    {
                        AgeRange = new { Min = profile.AgeRangeMin, Max = profile.AgeRangeMax },
                        MaxDistance = profile.MaxDistance,
                        University = profile.PreferredUniversityDomain ?? "Tüm Üniversiteler",
                        City = profile.PreferredCity ?? "Tüm Şehirler",
                        Department = profile.PreferredDepartment ?? "Tüm Bölümler"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateFilters hatası. UserId: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Günlük swipe limitini kontrol eder
        /// </summary>
        public async Task<bool> CheckDailySwipeLimit(string userId)
        {
            try
            {
                var profile = await _db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                    return false;

                await ResetDailySwipeCountIfNeeded(profile);

                // Premium unlimited
                if (profile.IsPremium)
                    return true;

                // Free user limit check
                return profile.DailySwipeCount < FREE_DAILY_SWIPE_LIMIT;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckDailySwipeLimit hatası. UserId: {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Super like limitini kontrol eder
        /// </summary>
        public async Task<bool> CheckSuperLikeLimit(string userId)
        {
            try
            {
                var profile = await _db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                    return false;

                // TODO: Günlük super like reset mantığı eklenebilir

                return profile.SuperLikeCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckSuperLikeLimit hatası. UserId: {UserId}", userId);
                return false;
            }
        }

    }
}
