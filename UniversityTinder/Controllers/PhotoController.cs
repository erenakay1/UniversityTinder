using AutoMapper;
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
    [Route("api/photo")]
    [ApiController]
    public class PhotoController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;
        private readonly IImageSensorService _imageSensorService;
        private readonly ILogger<PhotoController> _logger;
        private ResponseDto _responseDto;

        public PhotoController(
            AppDbContext db,
            IMapper mapper,
            IImageService imageService,
            IImageSensorService imageSensorService,
            ILogger<PhotoController> logger)
        {
            _db = db;
            _mapper = mapper;
            _imageService = imageService;
            _imageSensorService = imageSensorService;
            _logger = logger;
            _responseDto = new ResponseDto();
        }

        /// <summary>
        /// Yeni fotoğraf ekle (Maksimum 6 fotoğraf kontrolü ile)
        /// </summary>
        [HttpPost("AddPhoto")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> AddPhoto([FromForm] IFormFile photo, [FromForm] int order = 0)
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

                // Profil tamamlanmamışsa fotoğraf eklenemez
                if (!profile.IsProfileCompleted)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Önce profilinizi tamamlamalısınız";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                // Maksimum 6 fotoğraf kontrolü
                if (profile.PhotosList.Count >= 6)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Maksimum 6 fotoğraf yükleyebilirsiniz";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                if (photo == null)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Fotoğraf dosyası boş olamaz";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                _logger.LogInformation("Yeni fotoğraf ekleniyor. UserId: {UserId}", userId);

                // Resim analizi (Google Vision API)
                var analysisResult = await _imageSensorService.AnalyzeImageAsync(photo);
                if (!analysisResult.IsSuccess)
                {
                    _logger.LogWarning("Fotoğraf analizi başarısız: {Message}", analysisResult.Message);
                    _responseDto = analysisResult;
                    return _responseDto;
                }

                // Fotoğrafı S3'e kaydet
                var imageResult = _imageService.SaveImage(
                    photo,
                    "ProfilePhotos",
                    $"{profile.ProfileId}_{profile.PhotosList.Count + 1}_{DateTime.UtcNow.Ticks}"
                );

                _logger.LogInformation("Fotoğraf S3'e yüklendi: {ImageUrl}", imageResult.ImageUrl);

                // Photo entity oluştur
                var newPhoto = new Photo
                {
                    PhotoImageUrl = imageResult.ImageUrl,
                    PhotoImageLocalPath = imageResult.LocalPath, // S3 key
                    Order = order > 0 ? order : profile.PhotosList.Count + 1,
                    IsMainPhoto = false,
                    IsVerified = false,
                    UploadedAt = DateTime.UtcNow,
                    ProfileId = profile.ProfileId,
                    ImageStatus = "pending"
                };

                _db.Photos.Add(newPhoto);

                // PhotoImageStatus listesini güncelle
                var statusList = profile.PhotoImageStatus;
                statusList.Add("pending");
                profile.PhotoImageStatus = statusList;

                // Score'u güncelle
                profile.ProfileCompletionScore = CalculateCompletionScore(profile);
                profile.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation("Fotoğraf başarıyla eklendi. PhotoId: {PhotoId}", newPhoto.PhotoId);

                // Response için PhotoDto oluştur
                var photoDto = new PhotoDto
                {
                    PhotoId = newPhoto.PhotoId,
                    PhotoImageUrl = newPhoto.PhotoImageUrl,
                    PhotoImageLocalPath = newPhoto.PhotoImageLocalPath,
                    ImageStatus = newPhoto.ImageStatus,
                    Order = newPhoto.Order,
                    IsMainPhoto = newPhoto.IsMainPhoto,
                    IsVerified = newPhoto.IsVerified,
                    UploadedAt = newPhoto.UploadedAt,
                    ProfileId = newPhoto.ProfileId
                };

                _responseDto.Result = photoDto;
                _responseDto.IsSuccess = true;
                _responseDto.Message = "Fotoğraf başarıyla eklendi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fotoğraf ekleme hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Fotoğraf sil
        /// </summary>
        [HttpDelete("DeletePhoto/{photoId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> DeletePhoto(int photoId)
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

                var photo = profile.PhotosList.FirstOrDefault(p => p.PhotoId == photoId);

                if (photo == null)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Fotoğraf bulunamadı veya size ait değil";
                    _responseDto.StatusCode = HttpStatusCode.NotFound;
                    return _responseDto;
                }

                // En az 2 fotoğraf kalmalı kontrolü
                if (profile.PhotosList.Count <= 2)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "En az 2 fotoğrafınız olmalı. Silmek için önce yeni fotoğraf ekleyin.";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                _logger.LogInformation("Fotoğraf siliniyor. PhotoId: {PhotoId}, UserId: {UserId}",
                    photoId, userId);

                // Ana fotoğraf mı kontrol et
                bool wasMainPhoto = photo.IsMainPhoto;

                // ✅ S3'ten fotoğrafı sil (ASYNC)
                try
                {
                    if (!string.IsNullOrEmpty(photo.PhotoImageUrl))
                    {
                        var deleteSuccess = await _imageService.DeleteImageAsync(photo.PhotoImageUrl);

                        if (deleteSuccess)
                        {
                            _logger.LogInformation("S3'ten fotoğraf silindi: {ImageUrl}", photo.PhotoImageUrl);
                        }
                        else
                        {
                            _logger.LogWarning("S3'ten fotoğraf silinemedi (devam ediliyor): {ImageUrl}", photo.PhotoImageUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "S3'ten dosya silinirken hata oluştu (devam ediliyor): {ImageUrl}",
                        photo.PhotoImageUrl);
                    // Hata olsa bile devam et, veritabanından silinsin
                }

                // Veritabanından sil
                _db.Photos.Remove(photo);

                // Eğer ana fotoğraf silinmişse, ilk fotoğrafı ana yap
                if (wasMainPhoto && profile.PhotosList.Count > 1)
                {
                    var newMainPhoto = profile.PhotosList
                        .Where(p => p.PhotoId != photoId)
                        .OrderBy(p => p.Order)
                        .FirstOrDefault();

                    if (newMainPhoto != null)
                    {
                        newMainPhoto.IsMainPhoto = true;
                        profile.ProfileImageUrl = newMainPhoto.PhotoImageUrl;
                        profile.ProfileImageLocalPath = newMainPhoto.PhotoImageLocalPath;
                        _logger.LogDebug("Yeni ana fotoğraf ayarlandı: PhotoId={PhotoId}", newMainPhoto.PhotoId);
                    }
                }

                // PhotoImageStatus listesini güncelle
                var statusList = profile.PhotoImageStatus;
                var photoIndex = profile.PhotosList.IndexOf(photo);
                if (photoIndex >= 0 && photoIndex < statusList.Count)
                {
                    statusList.RemoveAt(photoIndex);
                    profile.PhotoImageStatus = statusList;
                }

                // Kalan fotoğrafların order'ını yeniden düzenle
                var remainingPhotos = profile.PhotosList
                    .Where(p => p.PhotoId != photoId)
                    .OrderBy(p => p.Order)
                    .ToList();

                for (int i = 0; i < remainingPhotos.Count; i++)
                {
                    remainingPhotos[i].Order = i + 1;
                }

                // Score'u güncelle
                profile.ProfileCompletionScore = CalculateCompletionScore(profile);
                profile.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation("Fotoğraf başarıyla silindi. PhotoId: {PhotoId}", photoId);

                _responseDto.IsSuccess = true;
                _responseDto.Message = "Fotoğraf başarıyla silindi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fotoğraf silme hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Ana profil fotoğrafını değiştir
        /// </summary>
        [HttpPut("SetMainPhoto/{photoId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> SetMainPhoto(int photoId)
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

                var photo = profile.PhotosList.FirstOrDefault(p => p.PhotoId == photoId);

                if (photo == null)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Fotoğraf bulunamadı veya size ait değil";
                    _responseDto.StatusCode = HttpStatusCode.NotFound;
                    return _responseDto;
                }

                // Zaten ana fotoğraf mı?
                if (photo.IsMainPhoto)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Bu fotoğraf zaten ana profil fotoğrafınız";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                _logger.LogInformation("Ana fotoğraf değiştiriliyor. PhotoId: {PhotoId}, UserId: {UserId}",
                    photoId, userId);

                // Eski ana fotoğrafı kaldır
                var oldMainPhoto = profile.PhotosList.FirstOrDefault(p => p.IsMainPhoto);
                if (oldMainPhoto != null)
                {
                    oldMainPhoto.IsMainPhoto = false;
                    _logger.LogDebug("Eski ana fotoğraf kaldırıldı: PhotoId={PhotoId}", oldMainPhoto.PhotoId);
                }

                // Yeni ana fotoğrafı ayarla
                photo.IsMainPhoto = true;
                profile.ProfileImageUrl = photo.PhotoImageUrl;
                profile.ProfileImageLocalPath = photo.PhotoImageLocalPath;
                profile.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation("Ana fotoğraf başarıyla değiştirildi. PhotoId: {PhotoId}", photoId);

                // Response için PhotoDto oluştur
                var photoDto = new PhotoDto
                {
                    PhotoId = photo.PhotoId,
                    PhotoImageUrl = photo.PhotoImageUrl,
                    PhotoImageLocalPath = photo.PhotoImageLocalPath,
                    ImageStatus = photo.ImageStatus,
                    Order = photo.Order,
                    IsMainPhoto = photo.IsMainPhoto,
                    IsVerified = photo.IsVerified,
                    UploadedAt = photo.UploadedAt,
                    ProfileId = photo.ProfileId
                };

                _responseDto.Result = photoDto;
                _responseDto.IsSuccess = true;
                _responseDto.Message = "Ana fotoğraf başarıyla değiştirildi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ana fotoğraf değiştirme hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        

        /// <summary>
        /// Fotoğraf sırasını değiştir
        /// </summary>
        [HttpPut("ReorderPhotos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> ReorderPhotos([FromBody] List<PhotoOrderDto> photoOrders)
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

                if (photoOrders == null || photoOrders.Count == 0)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Fotoğraf sıralaması boş olamaz";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                _logger.LogInformation("Fotoğraf sıralaması güncelleniyor. UserId: {UserId}", userId);

                // Her bir fotoğrafın sırasını güncelle
                foreach (var orderDto in photoOrders)
                {
                    var photo = profile.PhotosList.FirstOrDefault(p => p.PhotoId == orderDto.PhotoId);
                    if (photo != null)
                    {
                        photo.Order = orderDto.NewOrder;
                        _logger.LogDebug("PhotoId={PhotoId} → Order={Order}", orderDto.PhotoId, orderDto.NewOrder);
                    }
                }

                profile.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Fotoğraf sıralaması başarıyla güncellendi");

                // Güncel fotoğraf listesini döndür
                var updatedPhotos = profile.PhotosList
                    .OrderBy(p => p.Order)
                    .Select(p => new PhotoDto
                    {
                        PhotoId = p.PhotoId,
                        PhotoImageUrl = p.PhotoImageUrl,
                        PhotoImageLocalPath = p.PhotoImageLocalPath,
                        ImageStatus = p.ImageStatus,
                        Order = p.Order,
                        IsMainPhoto = p.IsMainPhoto,
                        IsVerified = p.IsVerified,
                        UploadedAt = p.UploadedAt,
                        ProfileId = p.ProfileId
                    })
                    .ToList();

                _responseDto.Result = updatedPhotos;
                _responseDto.IsSuccess = true;
                _responseDto.Message = "Fotoğraf sıralaması başarıyla güncellendi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fotoğraf sıralama hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Kullanıcının tüm fotoğraflarını getir
        /// </summary>
        [HttpGet("GetMyPhotos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> GetMyPhotos()
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

                var photos = profile.PhotosList
                    .OrderBy(p => p.Order)
                    .Select(p => new PhotoDto
                    {
                        PhotoId = p.PhotoId,
                        PhotoImageUrl = p.PhotoImageUrl,
                        PhotoImageLocalPath = p.PhotoImageLocalPath,
                        ImageStatus = p.ImageStatus,
                        Order = p.Order,
                        IsMainPhoto = p.IsMainPhoto,
                        IsVerified = p.IsVerified,
                        UploadedAt = p.UploadedAt,
                        ProfileId = p.ProfileId
                    })
                    .ToList();

                _responseDto.Result = photos;
                _responseDto.IsSuccess = true;
                _responseDto.Message = $"{photos.Count} fotoğraf bulundu";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fotoğraf listesi getirme hatası");
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


    }
}
