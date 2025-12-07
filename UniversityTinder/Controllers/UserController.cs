using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using UniversityTinder.Data;
using UniversityTinder.Models;
using UniversityTinder.Models.Dto;
using UniversityTinder.Models.Dto.UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly IAuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;
        protected ResponseDto _responseDto;
        public UserController(IAuthService authService, IConfiguration configuration, AppDbContext db,
            UserManager<ApplicationUser> userManager, ILogger<UserController> logger)
        {

            _configuration = configuration;
            _userManager = userManager;
            _authService = authService;
            _db = db;
            _responseDto = new();
            _logger = logger;
        }



        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequestDTO model)
        {
            try
            {
                var registerResponse = await _authService.Register(model);

                _logger.LogInformation("Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(Register), DateTime.Now);

                if (!(registerResponse.IsSuccess ?? false))
                {
                    _logger.LogWarning("Kayıt başarısız: {Message}", registerResponse.Message);
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = registerResponse.Message;
                    return BadRequest(_responseDto);
                }

                _logger.LogInformation("Kayıt başarılı: {Email}", model.Email);
                _responseDto.IsSuccess = true;
                _responseDto.Result = registerResponse;
                _responseDto.Message = registerResponse.Message;
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register hatası: {Message}", ex.Message);
                _responseDto.IsSuccess = false;
                _responseDto.Message = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
                return StatusCode(500, _responseDto);
            }
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO model)
        {
            var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null || (user.LockoutEnd != null && user.LockoutEnd > DateTime.Now))
            {
                _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(Login), DateTime.Now.ToString());
                _logger.LogInformation("Kullanıcı kilitli veya geçersiz kullanıcı adı/şifre.");

                _responseDto.IsSuccess = false;
                _responseDto.Message = "Kullanıcı kilitli veya geçersiz kullanıcı adı/şifre.";
                return BadRequest(_responseDto);
            }

            var loginResponse = await _authService.Login(model);

            if (loginResponse.User == null)
            {
                _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(Login), DateTime.Now.ToString());
                _logger.LogInformation("Kullanıcı adı veya şifre yanlış.");

                _responseDto.IsSuccess = false;
                _responseDto.Message = "Kullanıcı adı veya şifre yanlış.";
                return BadRequest(_responseDto);
            }

            _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                nameof(UserController), nameof(Login), DateTime.Now.ToString());
            _logger.LogInformation("Giriş başarıyla yapıldı.");

            _responseDto.IsSuccess = true;
            _responseDto.Result = loginResponse;
            _responseDto.Message = "Giriş başarıyla yapıldı.";
            return Ok(_responseDto);
        }


        [Authorize]
        [HttpDelete("DeleteUser")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequestDTO model)
        {
            try
            {
                var result = await _authService.DeleteUser(model.UserId, model.Password);

                _logger.LogInformation("Request received by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(DeleteUser), DateTime.Now.ToString());

                _responseDto.IsSuccess = true;
                _responseDto.Message = result;
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteUser işlemi sırasında hata: {Message}", ex.Message);
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Hesap silme işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
            }
        }

        [HttpPost("AssignRole")]
        public async Task<IActionResult> AssignRole([FromBody] RegistrationRequestDTO model)
        {
            var assignRoleSuccessfull = await _authService.AssignRole(model.Email, model.Role.ToUpper());
            if (!assignRoleSuccessfull)
            {
                _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(AssignRole), DateTime.Now.ToString());
                _logger.LogInformation("Error Encountered");

                _responseDto.IsSuccess = false;
                _responseDto.Message = "Rol atama işlemi başarısız oldu.";
                return BadRequest(_responseDto);
            }

            _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                nameof(UserController), nameof(AssignRole), DateTime.Now.ToString());
            _logger.LogInformation("Rol başarıyla atandı.");

            _responseDto.IsSuccess = true;
            _responseDto.Message = "Rol başarıyla atandı.";
            return Ok(_responseDto);
        }


        [Authorize]
        [HttpGet("GetUser/{userId}")]
        public async Task<IActionResult> GetUser(string userId)
        {
            try
            {
                var user = await _authService.GetUserById(userId);

                _logger.LogInformation("Request received by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(GetUser), DateTime.Now.ToString());

                _responseDto.IsSuccess = true;
                _responseDto.Result = user;
                _responseDto.Message = "Kullanıcı bilgileri başarıyla getirildi.";
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUser işlemi sırasında hata: {Message}", ex.Message);
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Kullanıcı getirme işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
            }
        }


        [Authorize]
        [HttpPut("UpdateUser")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequestDTO model)
        {
            try
            {
                var updatedUser = await _authService.UpdateUser(model);

                _logger.LogInformation("Request received by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(UpdateUser), DateTime.Now.ToString());

                _responseDto.IsSuccess = true;
                _responseDto.Result = updatedUser;
                _responseDto.Message = "Kullanıcı bilgileri başarıyla güncellendi.";
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateUser işlemi sırasında hata: {Message}", ex.Message);
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Güncelleme işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
            }
        }


        [Authorize]
        [HttpPut("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDTO model)
        {
            try
            {
                var updatedUser = await _authService.ChangePassword(model);

                _logger.LogInformation("Request received by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(ChangePassword), DateTime.Now.ToString());

                _responseDto.IsSuccess = true;
                _responseDto.Result = updatedUser; // Güncellenmiş kullanıcı bilgileri
                _responseDto.Message = "Şifre başarıyla değiştirildi";
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangePassword işlemi sırasında hata: {Message}", ex.Message);
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Şifre değiştirme işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
            }
        }


        //input a reset token in gonderilecegi email i giriyoruz
        //[Authorize]
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDTO model)
        {
            try
            {
                var result = await _authService.ForgotPassword(model);

                _responseDto.IsSuccess = true;
                _responseDto.Message = result;
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Şifre sıfırlama işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
            }
        }


        //email yerine hesabimizin emailini giriyoruz
        //[Authorize]
        [HttpPost("ResetPasswordWithCode")]
        public async Task<IActionResult> ResetPasswordWithCode([FromBody] ResetPasswordWithCodeRequestDTO model)
        {
            try
            {
                var result = await _authService.ResetPasswordWithCode(model);

                _responseDto.IsSuccess = true;
                _responseDto.Message = result;
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Şifre sıfırlama işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
            }
        }


        //[HttpGet("verify-email")]
        //[AllowAnonymous]
        //public async Task<IActionResult> VerifyEmail([FromQuery] string userId, [FromQuery] string token)
        //{
        //    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        //    {
        //        return BadRequest(new { IsSuccess = false, Message = "Geçersiz doğrulama linki" });
        //    }

        //    var user = await _userManager.FindByIdAsync(userId);
        //    if (user == null)
        //    {
        //        return NotFound(new { IsSuccess = false, Message = "Kullanıcı bulunamadı" });
        //    }

        //    if (user.EmailConfirmed)
        //    {
        //        return Ok(new { IsSuccess = true, Message = "Email zaten doğrulanmış" });
        //    }

        //    // Token'ı decode et
        //    var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

        //    var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        //    if (!result.Succeeded)
        //    {
        //        _logger.LogWarning("Email doğrulama başarısız: {UserId}, Hatalar: {Errors}",
        //            userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        //        return BadRequest(new { IsSuccess = false, Message = "Doğrulama başarısız. Link süresi dolmuş olabilir." });
        //    }

        //    // Üniversite doğrulamasını aktif et
        //    user.IsUniversityVerified = true;
        //    user.EmailVerifiedAt = DateTime.UtcNow;
        //    await _userManager.UpdateAsync(user);

        //    _logger.LogInformation("Email doğrulandı: {Email}", user.Email);

        //    // HTML response veya redirect
        //    var html = @"
        //        <!DOCTYPE html>
        //        <html>
        //        <head>
        //            <meta charset='utf-8'>
        //            <title>Email Doğrulandı</title>
        //            <style>
        //                body { font-family: Arial; display: flex; justify-content: center; align-items: center; height: 100vh; background: #f0f0f0; }
        //                .card { background: white; padding: 40px; border-radius: 10px; text-align: center; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        //                .success { color: #4CAF50; font-size: 48px; }
        //            </style>
        //        </head>
        //        <body>
        //            <div class='card'>
        //                <div class='success'>✓</div>
        //                <h1>Email Doğrulandı!</h1>
        //                <p>Hesabın aktif edildi. Artık uygulamaya giriş yapabilirsin.</p>
        //            </div>
        //        </body>
        //        </html>";

        //    return Content(html, "text/html");
        //}



        [HttpPost("VerifyEmailWithCode")]
        public async Task<IActionResult> VerifyEmailWithCode([FromBody] VerifyEmailWithCodeRequestDTO verifyRequest)
        {
            try
            {
                var message = await _authService.VerifyEmailWithCode(verifyRequest);
                _responseDto.Result = message;
                _responseDto.Message = message;
                _responseDto.IsSuccess = true;
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyEmailWithCode hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = ex.Message;
                return BadRequest(_responseDto);
            }
        }

        [HttpPost("ResendVerificationCode")]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendVerificationCodeRequestDTO resendRequest)
        {
            try
            {
                var message = await _authService.ResendVerificationCode(resendRequest);
                _responseDto.Result = message;
                _responseDto.Message = message;
                _responseDto.IsSuccess = true;
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResendVerificationCode hatası");
                _responseDto.IsSuccess = false;
                _responseDto.Message = ex.Message;
                return BadRequest(_responseDto);
            }
        }

    }
}
