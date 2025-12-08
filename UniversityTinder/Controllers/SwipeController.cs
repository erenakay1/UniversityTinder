using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Controllers
{
    [Route("api/swipe")]
    [ApiController]
    [Authorize]
    public class SwipeController : ControllerBase
    {
        private readonly ISwipeService _swipeService;
        private readonly ILogger<SwipeController> _logger;
        private ResponseDto _responseDto;

        public SwipeController(
            ISwipeService swipeService,
            ILogger<SwipeController> logger)
        {
            _swipeService = swipeService;
            _logger = logger;
            _responseDto = new ResponseDto();
        }

        /// <summary>
        /// Swipe edilebilir profilleri getirir (50 profil)
        /// </summary>
        [HttpGet("GetPotentialMatches")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> GetPotentialMatches()
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

                var profiles = await _swipeService.GetPotentialMatches(userId);

                _responseDto.Result = profiles;
                _responseDto.IsSuccess = true;
                _responseDto.Message = $"{profiles.Count} profil bulundu";
                _responseDto.StatusCode = HttpStatusCode.OK;

                _logger.LogInformation(
                    "GetPotentialMatches başarılı. UserId: {UserId}, Count: {Count}",
                    userId, profiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPotentialMatches hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Profili beğenir (sağa kaydırma)
        /// </summary>
        [HttpPost("Like")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> Like([FromBody] SwipeRequestDto request)
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

                var result = await _swipeService.Like(userId, request.TargetUserId);

                _responseDto.Result = result;
                _responseDto.IsSuccess = result.IsSuccess;
                _responseDto.Message = result.Message;
                _responseDto.StatusCode = result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest;

                if (result.IsMatch)
                {
                    _logger.LogInformation(
                        "🎉 MATCH! User1: {User1}, User2: {User2}",
                        userId, request.TargetUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Like işlemi hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Profili geçer (sola kaydırma)
        /// </summary>
        [HttpPost("Pass")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> Pass([FromBody] SwipeRequestDto request)
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

                var result = await _swipeService.Pass(userId, request.TargetUserId);

                _responseDto.Result = result;
                _responseDto.IsSuccess = result.IsSuccess;
                _responseDto.Message = result.Message;
                _responseDto.StatusCode = result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pass işlemi hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Super like (yukarı kaydırma)
        /// </summary>
        [HttpPost("SuperLike")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> SuperLike([FromBody] SwipeRequestDto request)
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

                var result = await _swipeService.SuperLike(userId, request.TargetUserId);

                _responseDto.Result = result;
                _responseDto.IsSuccess = result.IsSuccess;
                _responseDto.Message = result.Message;
                _responseDto.StatusCode = result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest;

                if (result.IsMatch)
                {
                    _logger.LogInformation(
                        "🌟 SUPER MATCH! User1: {User1}, User2: {User2}",
                        userId, request.TargetUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SuperLike işlemi hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Son swipe'ı geri alır (Premium only)
        /// </summary>
        [HttpPost("Undo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> UndoLastSwipe()
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

                var result = await _swipeService.UndoLastSwipe(userId);

                _responseDto.Result = result;
                _responseDto.IsSuccess = result.IsSuccess;
                _responseDto.Message = result.Message;
                _responseDto.StatusCode = result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Undo işlemi hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Swipe istatistiklerini getirir
        /// </summary>
        [HttpGet("Stats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> GetSwipeStats()
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

                var stats = await _swipeService.GetSwipeStats(userId);

                _responseDto.Result = stats;
                _responseDto.IsSuccess = true;
                _responseDto.Message = "İstatistikler getirildi";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSwipeStats hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }

        /// <summary>
        /// Premium kullanıcı filtrelerini günceller
        /// </summary>
        [HttpPut("UpdateFilters")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ResponseDto> UpdateFilters([FromBody] FilterUpdateDto filterDto)
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

                var result = await _swipeService.UpdateFilters(userId, filterDto);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateFilters hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
                return _responseDto;
            }
        }

        /// <summary>
        /// Günlük swipe limitini kontrol eder
        /// </summary>
        [HttpGet("CheckSwipeLimit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ResponseDto> CheckSwipeLimit()
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

                var hasSwipes = await _swipeService.CheckDailySwipeLimit(userId);

                _responseDto.Result = new { CanSwipe = hasSwipes };
                _responseDto.IsSuccess = true;
                _responseDto.Message = hasSwipes ? "Swipe yapabilirsiniz" : "Günlük limit doldu";
                _responseDto.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckSwipeLimit hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Bir hata oluştu: {ex.Message}";
                _responseDto.StatusCode = HttpStatusCode.InternalServerError;
            }

            return _responseDto;
        }
    }
}