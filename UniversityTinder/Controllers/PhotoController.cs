using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using UniversityTinder.Data;
using UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Controllers
{
    /// <summary>
    /// Fotoğraf okuma işlemleri (CRUD işlemleri ProfileController'da)
    /// </summary>
    [Route("api/photo")]
    [ApiController]
    [Authorize]
    public class PhotoController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;
        private readonly ILogger<PhotoController> _logger;
        private ResponseDto _responseDto;

        public PhotoController(
            AppDbContext db,
            IMapper mapper,
            ILogger<PhotoController> logger)
        {
            _db = db;
            _mapper = mapper;
            _logger = logger;
            _responseDto = new ResponseDto();
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
        /// Belirli bir fotoğrafın detayını getir
        /// </summary>
        [HttpGet("GetPhoto/{photoId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> GetPhoto(int photoId)
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
                _responseDto.Message = "Fotoğraf bulundu";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fotoğraf getirme hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }
    }
}