using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTinder.Data;
using UniversityTinder.Models;
using UniversityTinder.Models.Dto;
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

                if (registerResponse?.User == null)
                {
                    _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                        nameof(UserController), nameof(Register), DateTime.Now.ToString());
                    _logger.LogInformation("Kayıt oluşturulamadı");
                    _responseDto.IsSuccess = false;
                    _responseDto.Message = "Kayıt işlemi başarısız";
                    return BadRequest(_responseDto);
                }

                _logger.LogInformation("Request recieved by Controller: {Controller}, Action: {Action}, Datetime: {Datetime}",
                    nameof(UserController), nameof(Register), DateTime.Now.ToString());
                _logger.LogInformation("Kayıt başarıyla oluşturuldu.");

                _responseDto.IsSuccess = true;
                _responseDto.Result = registerResponse; // Burada result'ı set ediyoruz
                _responseDto.Message = "Kullanıcı kaydı başarıyla oluşturuldu.";
                return Ok(_responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register işlemi sırasında hata: {Message}", ex.Message);
                _responseDto.IsSuccess = false;
                _responseDto.Message = $"Kayıt işlemi başarısız: {ex.Message}";
                return BadRequest(_responseDto);
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
    }
}
