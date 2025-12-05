using AutoMapper;
using Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using UniversityTinder.Data;
using UniversityTinder.Models;
using UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Controllers
{
    [Route("api/profile")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;
        private readonly IImageSensorService _imageSensorService;
        private readonly ILogger<ProfileController> _logger;
        private ResponseDto _responseDto;

        public ProfileController(
            AppDbContext db,
            IMapper mapper,
            IImageService imageService,
            IImageSensorService imageSensorService,
            ILogger<ProfileController> logger)
        {
            _db = db;
            _mapper = mapper;
            _imageService = imageService;
            _imageSensorService = imageSensorService;
            _logger = logger;
            _responseDto = new ResponseDto();
        }

        /// <summary>
        /// İlk kayıt sonrası profil tamamlama (ZORUNLU)
        /// </summary>
        [HttpPost("CompleteProfile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> CompleteProfile([FromForm] ProfileCompleteDto completeDto)
        {
            try
            {
                // Token'dan userId çek
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("CompleteProfile: Token'da userId bulunamadı");
                    _responseDto.IsSuccess = false;
                    _responseDto.StatusCode = HttpStatusCode.Unauthorized;
                    _responseDto.Message = "Geçersiz kullanıcı token'ı";
                    return _responseDto;
                }

                _logger.LogInformation("Profile tamamlama işlemi başladı. UserId: {UserId}", userId);

                // Mevcut profili bul
                var profile = await _db.UserProfiles
                    .Include(p => p.User)
                    .Include(p => p.PhotosList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    _logger.LogWarning("Profile bulunamadı. UserId: {UserId}", userId);
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Profile bulunamadı. Lütfen önce kayıt olun.";
                    _responseDto.StatusCode = HttpStatusCode.NotFound;
                    return _responseDto;
                }

                // Zaten tamamlanmış profil kontrolü
                if (profile.IsProfileCompleted)
                {
                    _logger.LogWarning("Profile zaten tamamlanmış. UserId: {UserId}", userId);
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Profile zaten tamamlanmış. Güncellemek için UpdateProfile kullanın.";
                    _responseDto.StatusCode = HttpStatusCode.Conflict;
                    return _responseDto;
                }

                // Fotoğraf kontrolü (EN AZ 2, MAKS 6)
                if (completeDto.Photos == null || completeDto.Photos.Count < 2)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "En az 2 profil fotoğrafı yüklemelisiniz";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                if (completeDto.Photos.Count > 6)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "En fazla 6 fotoğraf yükleyebilirsiniz";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                // Temel bilgileri güncelle
                profile.Bio = completeDto.Bio;
                profile.Height = completeDto.Height;
                profile.Department = completeDto.Department;
                profile.YearOfStudy = completeDto.YearOfStudy;

                // Lokasyon bilgilerini güncelle
                profile.Latitude = completeDto.Latitude;
                profile.Longitude = completeDto.Longitude;
                profile.City = completeDto.City;
                profile.District = completeDto.District;
                profile.LastLocationUpdate = DateTime.UtcNow;

                // Dating preferences güncelle
                profile.InterestedIn = completeDto.InterestedIn;
                profile.AgeRangeMin = completeDto.AgeRangeMin;
                profile.AgeRangeMax = completeDto.AgeRangeMax;
                profile.MaxDistance = completeDto.MaxDistance;

                // Privacy settings güncelle
                profile.ShowMyUniversity = completeDto.ShowMyUniversity;
                profile.ShowMeOnApp = completeDto.ShowMeOnApp;
                profile.ShowDistance = completeDto.ShowDistance;
                profile.ShowAge = completeDto.ShowAge;

                _logger.LogInformation("Fotoğraflar işleniyor. Toplam: {Count}", completeDto.Photos.Count);

                // Fotoğrafları işle ve kaydet
                var photoStatusList = new List<string>();
                for (int i = 0; i < completeDto.Photos.Count; i++)
                {
                    var photoFile = completeDto.Photos[i];

                    _logger.LogDebug("Fotoğraf {Index} işleniyor", i + 1);

                    try
                    {
                        // Resim analizi (içerik kontrolü)
                        var analysisResult = await _imageSensorService.AnalyzeImageAsync(photoFile);
                        if (!analysisResult.IsSuccess)
                        {
                            _logger.LogWarning("Fotoğraf {Index} analizi başarısız: {Message}",
                                i + 1, analysisResult.Message);
                            _responseDto = analysisResult;
                            return _responseDto;
                        }

                        // Fotoğrafı kaydet
                        var imageResult = _imageService.SaveImage(
                            photoFile,
                            "ProfilePhotos",
                            $"{profile.ProfileId}_{i + 1}"
                        );

                        // Photo entity oluştur
                        var photo = new Photo
                        {
                            PhotoImageUrl = imageResult.ImageUrl,
                            PhotoImageLocalPath = imageResult.LocalPath,
                            Order = i + 1,
                            IsMainPhoto = (i == completeDto.MainPhotoIndex),
                            IsVerified = false, // Face verification sonrası true olacak
                            UploadedAt = DateTime.UtcNow,
                            ProfileId = profile.ProfileId,
                            ImageStatus = "pending" // "pending", "verified", "rejected"
                        };

                        _db.Photos.Add(photo);
                        photoStatusList.Add("pending");

                        // Ana profil fotoğrafını ProfileImageUrl'e de ekle
                        if (i == completeDto.MainPhotoIndex)
                        {
                            profile.ProfileImageUrl = imageResult.ImageUrl;
                            profile.ProfileImageLocalPath = imageResult.LocalPath;
                            _logger.LogDebug("Ana profil fotoğrafı ayarlandı: {ImageUrl}", imageResult.ImageUrl);
                        }

                        _logger.LogDebug("Fotoğraf {Index} başarıyla kaydedildi", i + 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fotoğraf {Index} işleme hatası", i + 1);
                        _responseDto.IsSuccess = false;
                        _responseDto.Message = $"Fotoğraf {i + 1} yüklenirken hata oluştu: {ex.Message}";
                        _responseDto.StatusCode = HttpStatusCode.InternalServerError;
                        return _responseDto;
                    }
                }

                // Photo status JSON'ını güncelle
                profile.PhotoImageStatus = photoStatusList;

                // Profile completion score hesapla
                profile.ProfileCompletionScore = CalculateCompletionScore(profile);

                // Profil tamamlandı işaretle
                profile.IsProfileCompleted = true;
                profile.UpdatedAt = DateTime.UtcNow;
                profile.LastActiveAt = DateTime.UtcNow;

                // Veritabanına kaydet
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Profile başarıyla tamamlandı. ProfileId: {ProfileId}, UserId: {UserId}, Score: {Score}",
                    profile.ProfileId, userId, profile.ProfileCompletionScore);

                // Response hazırla
                var profileDto = _mapper.Map<ProfileDto>(profile);

                _responseDto.Result = profileDto;
                _responseDto.IsSuccess = true;
                _responseDto.Message = "Profile başarıyla tamamlandı! Artık eşleşmeleri görmeye başlayabilirsiniz.";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profile tamamlama hatası: {ErrorMessage}. UserId: {UserId}",
                    ex.Message, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");

                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Profile tamamlama skoru hesaplama
        /// </summary>
        private int CalculateCompletionScore(UserProfile profile)
        {
            int score = 0;

            // Temel bilgiler (60 puan)
            if (!string.IsNullOrEmpty(profile.DisplayName)) score += 10;
            if (!string.IsNullOrEmpty(profile.Bio)) score += 15;
            if (profile.Height.HasValue) score += 10;
            if (!string.IsNullOrEmpty(profile.Department)) score += 10;
            if (profile.YearOfStudy.HasValue) score += 10;
            if (profile.Latitude.HasValue && profile.Longitude.HasValue) score += 5;

            // Fotoğraflar (40 puan)
            if (!string.IsNullOrEmpty(profile.ProfileImageUrl)) score += 10;

            var photoCount = profile.PhotosList?.Count ?? 0;
            if (photoCount >= 2) score += 10; // En az 2 fotoğraf
            if (photoCount >= 4) score += 10; // 4+ fotoğraf
            if (photoCount >= 6) score += 10; // Full 6 fotoğraf

            return Math.Min(score, 100);
        }


        /// <summary>
        /// Profil bilgilerini güncelle (Fotoğraf hariç)
        /// </summary>
        [HttpPut("UpdateProfile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> UpdateProfile([FromBody] ProfileUpdateDto updateDto)
        {
            try
            {
                // Token'dan userId çek
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("UpdateProfile: Token'da userId bulunamadı");
                    _responseDto.IsSuccess = false;
                    _responseDto.StatusCode = HttpStatusCode.Unauthorized;
                    _responseDto.Message = "Geçersiz kullanıcı token'ı";
                    return _responseDto;
                }

                _logger.LogInformation("Profile güncelleme işlemi başladı. UserId: {UserId}", userId);

                // Mevcut profili bul
                var profile = await _db.UserProfiles
                    .Include(p => p.User)
                    .Include(p => p.PhotosList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    _logger.LogWarning("Profile bulunamadı. UserId: {UserId}", userId);
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Profile bulunamadı";
                    _responseDto.StatusCode = HttpStatusCode.NotFound;
                    return _responseDto;
                }

                // Profil tamamlanmamışsa güncelleme yapılmasın
                if (!profile.IsProfileCompleted)
                {
                    _logger.LogWarning("Profil tamamlanmamış. Önce CompleteProfile kullanılmalı. UserId: {UserId}", userId);
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Profil henüz tamamlanmamış. Önce profili tamamlayın.";
                    _responseDto.StatusCode = HttpStatusCode.Forbidden;
                    return _responseDto;
                }

                // Sadece gönderilen alanları güncelle (null olmayan)
                if (updateDto.Bio != null)
                {
                    profile.Bio = updateDto.Bio;
                    _logger.LogDebug("Bio güncellendi");
                }

                if (updateDto.Height.HasValue)
                {
                    profile.Height = updateDto.Height.Value;
                    _logger.LogDebug("Height güncellendi: {Height}", updateDto.Height);
                }

                if (!string.IsNullOrEmpty(updateDto.Department))
                {
                    profile.Department = updateDto.Department;
                    _logger.LogDebug("Department güncellendi: {Department}", updateDto.Department);
                }

                if (updateDto.YearOfStudy.HasValue)
                {
                    profile.YearOfStudy = updateDto.YearOfStudy.Value;
                    _logger.LogDebug("YearOfStudy güncellendi: {YearOfStudy}", updateDto.YearOfStudy);
                }

                // Location güncelleme
                if (updateDto.Latitude.HasValue && updateDto.Longitude.HasValue)
                {
                    profile.Latitude = updateDto.Latitude.Value;
                    profile.Longitude = updateDto.Longitude.Value;
                    profile.City = updateDto.City;
                    profile.District = updateDto.District;
                    profile.LastLocationUpdate = DateTime.UtcNow;
                    _logger.LogDebug("Location güncellendi: Lat={Lat}, Lon={Lon}",
                        updateDto.Latitude, updateDto.Longitude);
                }

                // Dating preferences güncelleme
                if (!string.IsNullOrEmpty(updateDto.InterestedIn))
                {
                    profile.InterestedIn = updateDto.InterestedIn;
                    _logger.LogDebug("InterestedIn güncellendi: {InterestedIn}", updateDto.InterestedIn);
                }

                if (updateDto.AgeRangeMin.HasValue)
                {
                    profile.AgeRangeMin = updateDto.AgeRangeMin.Value;
                }

                if (updateDto.AgeRangeMax.HasValue)
                {
                    profile.AgeRangeMax = updateDto.AgeRangeMax.Value;
                }

                if (updateDto.MaxDistance.HasValue)
                {
                    profile.MaxDistance = updateDto.MaxDistance.Value;
                    _logger.LogDebug("MaxDistance güncellendi: {MaxDistance} km", updateDto.MaxDistance);
                }

                // Privacy settings güncelleme
                if (updateDto.ShowMyUniversity.HasValue)
                {
                    profile.ShowMyUniversity = updateDto.ShowMyUniversity.Value;
                }

                if (updateDto.ShowMeOnApp.HasValue)
                {
                    profile.ShowMeOnApp = updateDto.ShowMeOnApp.Value;
                }

                if (updateDto.ShowDistance.HasValue)
                {
                    profile.ShowDistance = updateDto.ShowDistance.Value;
                }

                if (updateDto.ShowAge.HasValue)
                {
                    profile.ShowAge = updateDto.ShowAge.Value;
                }

                // Social links güncelleme
                if (updateDto.InstagramUsername != null)
                {
                    profile.InstagramUsername = updateDto.InstagramUsername;
                    _logger.LogDebug("Instagram güncellendi: {Instagram}", updateDto.InstagramUsername);
                }

                // Profile completion score'u yeniden hesapla
                profile.ProfileCompletionScore = CalculateCompletionScore(profile);
                profile.UpdatedAt = DateTime.UtcNow;
                profile.LastActiveAt = DateTime.UtcNow;

                // Veritabanına kaydet
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Profile başarıyla güncellendi. ProfileId: {ProfileId}, UserId: {UserId}",
                    profile.ProfileId, userId);

                // Response hazırla
                var profileDto = _mapper.Map<ProfileDto>(profile);

                _responseDto.Result = profileDto;
                _responseDto.IsSuccess = true;
                _responseDto.Message = "Profile başarıyla güncellendi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profile güncelleme hatası: {ErrorMessage}. UserId: {UserId}",
                    ex.Message, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");

                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }



        /// <summary>
        /// Kullanıcı profilini tamamen sil (Hesap silme işlemi için)
        /// Tüm fotoğrafları S3'ten siler
        /// </summary>
        [HttpDelete("DeleteAccount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> DeleteAccount()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.StatusCode = HttpStatusCode.Unauthorized;
                    _responseDto.Message = "Geçersiz kullanıcı token'ı";
                    return _responseDto;
                }

                var profile = await _db.UserProfiles
                    .Include(p => p.PhotosList)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Profile bulunamadı";
                    _responseDto.StatusCode = HttpStatusCode.NotFound;
                    return _responseDto;
                }

                _logger.LogInformation("Hesap silme işlemi başlatılıyor. UserId: {UserId}, ProfileId: {ProfileId}",
                    userId, profile.ProfileId);

                // ✅ Tüm fotoğrafları S3'ten sil (prefix ile toplu silme)
                try
                {
                    var prefix = $"ProfilePhotos/{profile.ProfileId}_";
                    var deleteSuccess = await _imageService.DeleteImagesByPrefixAsync(prefix);

                    if (deleteSuccess)
                    {
                        _logger.LogInformation("S3'ten tüm fotoğraflar silindi. Prefix: {Prefix}", prefix);
                    }
                    else
                    {
                        _logger.LogWarning("S3'ten bazı fotoğraflar silinemedi (devam ediliyor). Prefix: {Prefix}", prefix);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "S3'ten fotoğraflar silinirken hata oluştu (devam ediliyor)");
                }

                // Veritabanından profili ve fotoğrafları sil
                _db.Photos.RemoveRange(profile.PhotosList);
                _db.UserProfiles.Remove(profile);

                await _db.SaveChangesAsync();

                _logger.LogInformation("Hesap başarıyla silindi. UserId: {UserId}", userId);

                _responseDto.IsSuccess = true;
                _responseDto.Message = "Hesabınız başarıyla silindi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hesap silme hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }


    }
}
