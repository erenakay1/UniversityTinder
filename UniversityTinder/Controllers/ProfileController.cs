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
        private readonly IFaceVerificationService _faceVerificationService;
        private readonly ILogger<ProfileController> _logger;
        private ResponseDto _responseDto;

        public ProfileController(
            AppDbContext db,
            IMapper mapper,
            IImageService imageService,
            IImageSensorService imageSensorService,
            IFaceVerificationService faceVerificationService,
            ILogger<ProfileController> logger)
        {
            _db = db;
            _mapper = mapper;
            _imageService = imageService;
            _imageSensorService = imageSensorService;
            _faceVerificationService = faceVerificationService;
            _logger = logger;
            _responseDto = new ResponseDto();
        }

        /// <summary>
        /// İlk kayıt sonrası profil tamamlama (ZORUNLU) - Face verification ile
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

                // ============================================
                // FACE VERIFICATION WORKFLOW
                // ============================================

                // Referans fotoğraf (ana fotoğraf - MainPhotoIndex)
                var mainPhotoFile = completeDto.Photos[completeDto.MainPhotoIndex];

                _logger.LogInformation("Ana fotoğraf (referans) işleniyor. Index: {Index}", completeDto.MainPhotoIndex);

                // Ana fotoğrafta yüz var mı kontrol et
                var mainFaceDetection = await _faceVerificationService.DetectFaceAsync(mainPhotoFile);

                if (!mainFaceDetection.IsSuccess || !mainFaceDetection.FaceDetected)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Ana fotoğrafınızda yüz tespit edilemedi. Lütfen net bir yüz fotoğrafı yükleyin.";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                if (mainFaceDetection.FaceCount > 1)
                {
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Ana fotoğrafınızda birden fazla yüz tespit edildi. Lütfen sadece sizin olduğunuz bir fotoğraf yükleyin.";
                    _responseDto.StatusCode = HttpStatusCode.BadRequest;
                    return _responseDto;
                }

                _logger.LogInformation("Ana fotoğrafta yüz tespit edildi. Confidence: {Confidence}%", mainFaceDetection.Confidence);

                // Fotoğrafları işle ve kaydet
                var photoStatusList = new List<string>();
                var photoEntities = new List<Photo>();

                for (int i = 0; i < completeDto.Photos.Count; i++)
                {
                    var photoFile = completeDto.Photos[i];
                    var isMainPhoto = (i == completeDto.MainPhotoIndex);

                    _logger.LogDebug("Fotoğraf {Index} işleniyor (IsMain: {IsMain})", i + 1, isMainPhoto);

                    try
                    {
                        // 1. Resim analizi (içerik kontrolü - Google Vision)
                        var analysisResult = await _imageSensorService.AnalyzeImageAsync(photoFile);
                        if (!analysisResult.IsSuccess)
                        {
                            _logger.LogWarning("Fotoğraf {Index} analizi başarısız: {Message}",
                                i + 1, analysisResult.Message);
                            _responseDto = analysisResult;
                            return _responseDto;
                        }

                        // 2. Ana fotoğraf değilse, face verification yap
                        string imageStatus = "pending";
                        bool isVerified = false;

                        if (!isMainPhoto)
                        {
                            _logger.LogDebug("Fotoğraf {Index} için face verification yapılıyor", i + 1);

                            var verificationResult = await _faceVerificationService.CompareFacesAsync(
                                mainPhotoFile,
                                photoFile
                            );

                            if (!verificationResult.IsSuccess)
                            {
                                _logger.LogWarning("Fotoğraf {Index} face verification başarısız: {Error}",
                                    i + 1, verificationResult.ErrorMessage);
                                imageStatus = "rejected";
                                isVerified = false;
                            }
                            else if (verificationResult.IsMatch)
                            {
                                _logger.LogInformation("Fotoğraf {Index} doğrulandı. Similarity: {Similarity}%",
                                    i + 1, verificationResult.Similarity);
                                imageStatus = "verified";
                                isVerified = true;
                            }
                            else
                            {
                                _logger.LogWarning("Fotoğraf {Index} doğrulanamadı. Similarity: {Similarity}% (Eşik: 80%)",
                                    i + 1, verificationResult.Similarity);

                                _responseDto.IsSuccess = false;
                                _responseDto.Message = $"Fotoğraf {i + 1} ana fotoğrafınızla eşleşmiyor. Lütfen sadece size ait fotoğraflar yükleyin. (Benzerlik: %{verificationResult.Similarity:F2})";
                                _responseDto.StatusCode = HttpStatusCode.BadRequest;
                                return _responseDto;
                            }
                        }
                        else
                        {
                            // Ana fotoğraf otomatik verified
                            imageStatus = "verified";
                            isVerified = true;
                            _logger.LogDebug("Ana fotoğraf otomatik doğrulandı");
                        }

                        // 3. Fotoğrafı kaydet
                        var imageResult = _imageService.SaveImage(
                            photoFile,
                            "ProfilePhotos",
                            $"{profile.ProfileId}_{i + 1}"
                        );

                        // 4. Photo entity oluştur
                        var photo = new Photo
                        {
                            PhotoImageUrl = imageResult.ImageUrl,
                            PhotoImageLocalPath = imageResult.LocalPath,
                            Order = i + 1,
                            IsMainPhoto = isMainPhoto,
                            IsVerified = isVerified,
                            UploadedAt = DateTime.UtcNow,
                            ProfileId = profile.ProfileId,
                            ImageStatus = imageStatus
                        };

                        photoEntities.Add(photo);
                        photoStatusList.Add(imageStatus);

                        // Ana profil fotoğrafını ProfileImageUrl'e de ekle
                        if (isMainPhoto)
                        {
                            profile.ProfileImageUrl = imageResult.ImageUrl;
                            profile.ProfileImageLocalPath = imageResult.LocalPath;
                            _logger.LogDebug("Ana profil fotoğrafı ayarlandı: {ImageUrl}", imageResult.ImageUrl);
                        }

                        _logger.LogDebug("Fotoğraf {Index} başarıyla kaydedildi (Status: {Status}, Verified: {Verified})",
                            i + 1, imageStatus, isVerified);
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

                // Tüm fotoğrafları veritabanına ekle
                _db.Photos.AddRange(photoEntities);

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

                var verifiedCount = photoEntities.Count(p => p.IsVerified);
                var rejectedCount = photoEntities.Count(p => p.ImageStatus == "rejected");

                _logger.LogInformation(
                    "Profile başarıyla tamamlandı. ProfileId: {ProfileId}, UserId: {UserId}, Score: {Score}, Verified: {Verified}/{Total}",
                    profile.ProfileId, userId, profile.ProfileCompletionScore, verifiedCount, photoEntities.Count);

                // Response hazırla
                var profileDto = _mapper.Map<ProfileDto>(profile);

                _responseDto.Result = profileDto;
                _responseDto.IsSuccess = true;
                _responseDto.Message = $"Profile başarıyla tamamlandı! {verifiedCount}/{photoEntities.Count} fotoğraf doğrulandı. Artık eşleşmeleri görmeye başlayabilirsiniz.";
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
        /// Profil bilgilerini güncelle (Temel bilgiler + Fotoğraf yönetimi)
        /// </summary>
        [HttpPut("UpdateProfile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> UpdateProfile([FromForm] ProfileUpdateDto updateDto)
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

                // ============================================
                // 1. TEMEL BİLGİLER GÜNCELLEMESİ
                // ============================================
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

                // ============================================
                // 2. FOTOĞRAF YÖNETİMİ + FACE VERIFICATION
                // ============================================

                // 2.1. YENİ FOTOĞRAF EKLEME (FACE VERIFICATION İLE)
                if (updateDto.NewPhotos != null && updateDto.NewPhotos.Count > 0)
                {
                    _logger.LogInformation("Yeni fotoğraflar ekleniyor. Adet: {Count}", updateDto.NewPhotos.Count);

                    // Maksimum 6 fotoğraf kontrolü
                    int currentPhotoCount = profile.PhotosList.Count;
                    int totalAfterAdd = currentPhotoCount + updateDto.NewPhotos.Count;

                    if (totalAfterAdd > 6)
                    {
                        _responseDto.IsSuccess = false;
                        _responseDto.Message = $"Maksimum 6 fotoğraf yükleyebilirsiniz. Şu anda {currentPhotoCount} fotoğrafınız var.";
                        _responseDto.StatusCode = HttpStatusCode.BadRequest;
                        return _responseDto;
                    }

                    // ============================================
                    // FACE VERIFICATION: Ana fotoğrafı bul
                    // ============================================
                    var mainPhoto = profile.PhotosList.FirstOrDefault(p => p.IsMainPhoto);

                    if (mainPhoto == null)
                    {
                        _logger.LogError("Ana fotoğraf bulunamadı! ProfileId: {ProfileId}", profile.ProfileId);
                        _responseDto.IsSuccess = false;
                        _responseDto.Message = "Ana fotoğraf bulunamadı. Lütfen destek ile iletişime geçin.";
                        _responseDto.StatusCode = HttpStatusCode.InternalServerError;
                        return _responseDto;
                    }

                    // Ana fotoğrafı S3'ten indir (karşılaştırma için)
                    IFormFile mainPhotoFile;
                    try
                    {
                        var mainPhotoBytes = await _imageService.GetImageBytesAsync(mainPhoto.PhotoImageUrl);
                        if (mainPhotoBytes == null)
                        {
                            _logger.LogError("Ana fotoğraf S3'ten indirilemedi: {Url}", mainPhoto.PhotoImageUrl);
                            _responseDto.IsSuccess = false;
                            _responseDto.Message = "Ana fotoğraf yüklenemedi. Lütfen tekrar deneyin.";
                            _responseDto.StatusCode = HttpStatusCode.InternalServerError;
                            return _responseDto;
                        }

                        // Byte array'i IFormFile'a çevir
                        var stream = new MemoryStream(mainPhotoBytes);
                        mainPhotoFile = new FormFile(stream, 0, mainPhotoBytes.Length, "mainPhoto", "mainPhoto.jpg")
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = "image/jpeg"
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ana fotoğraf yüklenirken hata oluştu");
                        _responseDto.IsSuccess = false;
                        _responseDto.Message = "Ana fotoğraf yüklenemedi. Lütfen tekrar deneyin.";
                        _responseDto.StatusCode = HttpStatusCode.InternalServerError;
                        return _responseDto;
                    }

                    var photoStatusList = profile.PhotoImageStatus.ToList();

                    for (int i = 0; i < updateDto.NewPhotos.Count; i++)
                    {
                        var photoFile = updateDto.NewPhotos[i];

                        try
                        {
                            _logger.LogDebug("Yeni fotoğraf {Index} işleniyor", i + 1);

                            // 1. Resim analizi (Google Vision - içerik kontrolü)
                            var analysisResult = await _imageSensorService.AnalyzeImageAsync(photoFile);
                            if (!analysisResult.IsSuccess)
                            {
                                _logger.LogWarning("Yeni fotoğraf {Index} analizi başarısız: {Message}",
                                    i + 1, analysisResult.Message);
                                _responseDto = analysisResult;
                                return _responseDto;
                            }

                            // ============================================
                            // 2. FACE VERIFICATION (YENİ EKLENEN!)
                            // ============================================
                            _logger.LogDebug("Yeni fotoğraf {Index} için face verification yapılıyor", i + 1);

                            var verificationResult = await _faceVerificationService.CompareFacesAsync(
                                mainPhotoFile,
                                photoFile
                            );

                            string imageStatus;
                            bool isVerified;

                            if (!verificationResult.IsSuccess)
                            {
                                _logger.LogWarning("Yeni fotoğraf {Index} face verification başarısız: {Error}",
                                    i + 1, verificationResult.ErrorMessage);
                                imageStatus = "rejected";
                                isVerified = false;

                                // Fotoğrafta yüz yoksa veya API hatası
                                _responseDto.IsSuccess = false;
                                _responseDto.Message = $"Fotoğraf {i + 1}: {verificationResult.ErrorMessage}";
                                _responseDto.StatusCode = HttpStatusCode.BadRequest;
                                return _responseDto;
                            }
                            else if (verificationResult.IsMatch)
                            {
                                _logger.LogInformation("Yeni fotoğraf {Index} doğrulandı. Similarity: {Similarity}%",
                                    i + 1, verificationResult.Similarity);
                                imageStatus = "verified";
                                isVerified = true;
                            }
                            else
                            {
                                _logger.LogWarning("Yeni fotoğraf {Index} doğrulanamadı. Similarity: {Similarity}% (Eşik: 80%)",
                                    i + 1, verificationResult.Similarity);

                                _responseDto.IsSuccess = false;
                                _responseDto.Message = $"Fotoğraf {i + 1} ana fotoğrafınızla eşleşmiyor. Lütfen sadece size ait fotoğraflar yükleyin. (Benzerlik: %{verificationResult.Similarity:F2})";
                                _responseDto.StatusCode = HttpStatusCode.BadRequest;
                                return _responseDto;
                            }

                            // 3. Fotoğrafı S3'e kaydet
                            var imageResult = _imageService.SaveImage(
                                photoFile,
                                "ProfilePhotos",
                                $"{profile.ProfileId}_{currentPhotoCount + i + 1}_{DateTime.UtcNow.Ticks}"
                            );

                            _logger.LogInformation("Yeni fotoğraf {Index} S3'e yüklendi: {Url}",
                                i + 1, imageResult.ImageUrl);

                            // 4. Photo entity oluştur
                            var photo = new Photo
                            {
                                PhotoImageUrl = imageResult.ImageUrl,
                                PhotoImageLocalPath = imageResult.LocalPath,
                                Order = currentPhotoCount + i + 1,
                                IsMainPhoto = false,
                                IsVerified = isVerified,  // ✅ FACE VERIFICATION SONUCU
                                UploadedAt = DateTime.UtcNow,
                                ProfileId = profile.ProfileId,
                                ImageStatus = imageStatus  // ✅ "verified" veya "rejected"
                            };

                            _db.Photos.Add(photo);
                            photoStatusList.Add(imageStatus);

                            _logger.LogDebug("Yeni fotoğraf {Index} başarıyla eklendi (Status: {Status}, Verified: {Verified})",
                                i + 1, imageStatus, isVerified);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Yeni fotoğraf {Index} ekleme hatası", i + 1);
                            _responseDto.IsSuccess = false;
                            _responseDto.Message = $"Fotoğraf {i + 1} yüklenirken hata oluştu: {ex.Message}";
                            _responseDto.StatusCode = HttpStatusCode.InternalServerError;
                            return _responseDto;
                        }
                    }

                    profile.PhotoImageStatus = photoStatusList;
                }

                // 2.2. FOTOĞRAF SİLME
                if (updateDto.PhotoIdsToDelete != null && updateDto.PhotoIdsToDelete.Count > 0)
                {
                    _logger.LogInformation("Fotoğraflar siliniyor. Adet: {Count}", updateDto.PhotoIdsToDelete.Count);

                    foreach (var photoId in updateDto.PhotoIdsToDelete)
                    {
                        var photo = profile.PhotosList.FirstOrDefault(p => p.PhotoId == photoId);

                        if (photo == null)
                        {
                            _logger.LogWarning("Silinecek fotoğraf bulunamadı: PhotoId={PhotoId}", photoId);
                            continue;
                        }

                        // En az 2 fotoğraf kalmalı kontrolü
                        int remainingPhotos = profile.PhotosList.Count - updateDto.PhotoIdsToDelete.Count;
                        if (remainingPhotos < 2)
                        {
                            _responseDto.IsSuccess = false;
                            _responseDto.Message = "En az 2 fotoğrafınız olmalı. Silmek için önce yeni fotoğraf ekleyin.";
                            _responseDto.StatusCode = HttpStatusCode.BadRequest;
                            return _responseDto;
                        }

                        bool wasMainPhoto = photo.IsMainPhoto;

                        // S3'ten fotoğrafı sil
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
                        }

                        // Veritabanından sil
                        _db.Photos.Remove(photo);

                        // Eğer ana fotoğraf silinmişse, ilk fotoğrafı ana yap
                        if (wasMainPhoto)
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

                        _logger.LogDebug("Fotoğraf silindi: PhotoId={PhotoId}", photoId);
                    }

                    // PhotoImageStatus listesini güncelle
                    var statusList = profile.PhotoImageStatus.ToList();
                    var photosToRemoveCount = updateDto.PhotoIdsToDelete.Count;

                    // Son eklenen fotoğrafların statuslerini sil
                    if (statusList.Count >= photosToRemoveCount)
                    {
                        statusList.RemoveRange(statusList.Count - photosToRemoveCount, photosToRemoveCount);
                        profile.PhotoImageStatus = statusList;
                    }

                    // Kalan fotoğrafların order'ını yeniden düzenle
                    var remainingPhotosList = profile.PhotosList
                        .Where(p => !updateDto.PhotoIdsToDelete.Contains(p.PhotoId))
                        .OrderBy(p => p.Order)
                        .ToList();

                    for (int i = 0; i < remainingPhotosList.Count; i++)
                    {
                        remainingPhotosList[i].Order = i + 1;
                    }
                }

                // 2.3. ANA FOTOĞRAF DEĞİŞTİRME
                if (updateDto.NewMainPhotoId.HasValue)
                {
                    _logger.LogInformation("Ana fotoğraf değiştiriliyor. PhotoId: {PhotoId}", updateDto.NewMainPhotoId);

                    var newMainPhoto = profile.PhotosList.FirstOrDefault(p => p.PhotoId == updateDto.NewMainPhotoId.Value);

                    if (newMainPhoto == null)
                    {
                        _responseDto.IsSuccess = false;
                        _responseDto.Message = "Belirtilen fotoğraf bulunamadı veya size ait değil";
                        _responseDto.StatusCode = HttpStatusCode.NotFound;
                        return _responseDto;
                    }

                    // Eski ana fotoğrafı kaldır
                    var oldMainPhoto = profile.PhotosList.FirstOrDefault(p => p.IsMainPhoto);
                    if (oldMainPhoto != null)
                    {
                        oldMainPhoto.IsMainPhoto = false;
                        _logger.LogDebug("Eski ana fotoğraf kaldırıldı: PhotoId={PhotoId}", oldMainPhoto.PhotoId);
                    }

                    // Yeni ana fotoğrafı ayarla
                    newMainPhoto.IsMainPhoto = true;
                    profile.ProfileImageUrl = newMainPhoto.PhotoImageUrl;
                    profile.ProfileImageLocalPath = newMainPhoto.PhotoImageLocalPath;

                    _logger.LogDebug("Yeni ana fotoğraf ayarlandı: PhotoId={PhotoId}", newMainPhoto.PhotoId);
                }

                // 2.4. FOTOĞRAF SIRALAMA
                if (updateDto.PhotoOrders != null && updateDto.PhotoOrders.Count > 0)
                {
                    _logger.LogInformation("Fotoğraf sıralaması güncelleniyor");

                    foreach (var orderDto in updateDto.PhotoOrders)
                    {
                        var photo = profile.PhotosList.FirstOrDefault(p => p.PhotoId == orderDto.PhotoId);
                        if (photo != null)
                        {
                            photo.Order = orderDto.NewOrder;
                            _logger.LogDebug("PhotoId={PhotoId} → Order={Order}", orderDto.PhotoId, orderDto.NewOrder);
                        }
                    }
                }

                // ============================================
                // 3. FİNAL İŞLEMLER
                // ============================================

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
        /// Kullanıcı profilini tamamen sil (Hesap silme işlemi için)
        /// Tüm fotoğrafları S3'ten siler
        /// </summary>
        //[HttpDelete("DeleteAccount")]
        //[ProducesResponseType(StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status401Unauthorized)]
        //[ProducesResponseType(StatusCodes.Status404NotFound)]
        //[ProducesResponseType(StatusCodes.Status500InternalServerError)]
        //public async Task<ResponseDto> DeleteAccount()
        //{
        //    try
        //    {
        //        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            _responseDto.IsSuccess = false;
        //            _responseDto.StatusCode = HttpStatusCode.Unauthorized;
        //            _responseDto.Message = "Geçersiz kullanıcı token'ı";
        //            return _responseDto;
        //        }

        //        var profile = await _db.UserProfiles
        //            .Include(p => p.PhotosList)
        //            .FirstOrDefaultAsync(p => p.UserId == userId);

        //        if (profile == null)
        //        {
        //            _responseDto.IsSuccess = false;
        //            _responseDto.Message = "Profile bulunamadı";
        //            _responseDto.StatusCode = HttpStatusCode.NotFound;
        //            return _responseDto;
        //        }

        //        _logger.LogInformation("Hesap silme işlemi başlatılıyor. UserId: {UserId}, ProfileId: {ProfileId}",
        //            userId, profile.ProfileId);

        //        // ✅ Tüm fotoğrafları S3'ten sil (prefix ile toplu silme)
        //        try
        //        {
        //            var prefix = $"ProfilePhotos/{profile.ProfileId}_";
        //            var deleteSuccess = await _imageService.DeleteImagesByPrefixAsync(prefix);

        //            if (deleteSuccess)
        //            {
        //                _logger.LogInformation("S3'ten tüm fotoğraflar silindi. Prefix: {Prefix}", prefix);
        //            }
        //            else
        //            {
        //                _logger.LogWarning("S3'ten bazı fotoğraflar silinemedi (devam ediliyor). Prefix: {Prefix}", prefix);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogWarning(ex, "S3'ten fotoğraflar silinirken hata oluştu (devam ediliyor)");
        //        }

        //        // Veritabanından profili ve fotoğrafları sil
        //        _db.Photos.RemoveRange(profile.PhotosList);
        //        _db.UserProfiles.Remove(profile);

        //        await _db.SaveChangesAsync();

        //        _logger.LogInformation("Hesap başarıyla silindi. UserId: {UserId}", userId);

        //        _responseDto.IsSuccess = true;
        //        _responseDto.Message = "Hesabınız başarıyla silindi";
        //        _responseDto.StatusCode = HttpStatusCode.OK;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Hesap silme hatası");
        //        _responseDto.IsSuccess = false;
        //        _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
        //        _responseDto.StatusCode = HttpStatusCode.InternalServerError;
        //    }

        //    return _responseDto;
        //}



    }
}