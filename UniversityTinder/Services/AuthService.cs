using Amazon.Runtime.Internal.Transform;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using UniversityTinder.Data;
using UniversityTinder.Models;
using UniversityTinder.Models.Dto;
using UniversityTinder.Models.Dto.UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;
using Utility;

namespace UniversityTinder.Services
{
    public class AuthService : IAuthService
    {

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IPasswordResetCodeService _passwordResetCodeService;
        private IMapper _mapper;
        private readonly IEmailVerificationCodeService _emailVerificationCodeService;
        private readonly IEmailService _emailService;
        private ILogger<AuthService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IImageService _imageService;

        public AuthService(AppDbContext db, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtTokenGenerator jwtTokenGenerator,
            IMapper mapper,
            IEmailVerificationCodeService emailVerificationCodeService,
            ILogger<AuthService> logger,
            IPasswordResetCodeService passwordResetCodeService,
            IMemoryCache cache,
            IImageService imageService, IEmailService emailService)
        {
            _db = db;
            _mapper = mapper;
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _emailVerificationCodeService = emailVerificationCodeService;
            _logger = logger;
            _passwordResetCodeService = passwordResetCodeService;
            _cache = cache;
            _imageService = imageService;
            _emailService = emailService;
        }

        public async Task<bool> AssignRole(string email, string roleName)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.Email.ToLower() == email.ToLower());
            if (user != null)
            {
                if (!_roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
                {
                    _roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
                }
                await _userManager.AddToRoleAsync(user, roleName);
                return true;
            }
            return false;
        }

        public async Task<UserDto> ChangePassword(ChangePasswordRequestDTO changePasswordRequest)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == changePasswordRequest.UserId);
                if (user == null)
                {
                    throw new Exception("Kullanıcı bulunamadı");
                }

                var result = await _userManager.ChangePasswordAsync(user, changePasswordRequest.CurrentPassword, changePasswordRequest.NewPassword);
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.FirstOrDefault()?.Description);
                }

                // Şifre değiştirildikten sonra güncel kullanıcı bilgilerini döndür
                var updatedUser = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == changePasswordRequest.UserId);
                var userDto = _mapper.Map<UserDto>(updatedUser);

                // Null değerleri temizle
                userDto.Name = userDto.Name ?? "";
                userDto.Surname = userDto.Surname ?? "";
                userDto.Gender = userDto.Gender ?? "";
                userDto.Role = userDto.Role ?? "";

                return userDto;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<string> DeleteUser(string userId, string password)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var user = await _db.ApplicationUsers
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    throw new Exception("Kullanıcı bulunamadı");
                }

                // Şifre kontrolü
                bool isValidPassword = await _userManager.CheckPasswordAsync(user, password);
                if (!isValidPassword)
                {
                    throw new Exception("Şifre hatalı");
                }

                _logger.LogInformation("Kullanıcı silme işlemi başlatıldı: {UserId}, Email: {Email}",
                    userId, user.Email);

                // ===== 1. PROFILE'I VE RESİMLERİ SİL =====
                var profile = await _db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                int totalPhotosDeleted = 0;

                if (profile != null)
                {
                    // Profile resmini sil
                    try
                    {
                        if (!string.IsNullOrEmpty(profile.ProfileImageUrl))
                        {
                            await _imageService.DeleteImageAsync(profile.ProfileImageUrl);
                            totalPhotosDeleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Profile resmi silinirken hata");
                    }

                    // Tüm fotoğrafları sil (PhotosList'ten)
                    if (profile.PhotosList != null && profile.PhotosList.Any())
                    {
                        foreach (var photo in profile.PhotosList)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(photo.PhotoImageUrl))
                                {
                                    await _imageService.DeleteImageAsync(photo.PhotoImageUrl);
                                    totalPhotosDeleted++;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Fotoğraf silinirken hata: {PhotoId}", photo.PhotoId);
                            }
                        }
                    }

                    _logger.LogInformation("{Count} fotoğraf S3'ten silindi", totalPhotosDeleted);
                }

                // ===== 2. DİĞER KULLANICILARIN PROFİLLERİNDEN TEMİZLE =====
                _db.ChangeTracker.Clear();

                var allProfiles = await _db.UserProfiles
                    .Where(p => p.UserId != userId)
                    .ToListAsync();

                int cleanedProfileCount = 0;
                foreach (var otherProfile in allProfiles)
                {
                    int removedCount = 0;

                    // MatchesList'ten çıkar
                    if (otherProfile.MatchesList != null)
                    {
                        var beforeCount = otherProfile.MatchesList.Count;
                        otherProfile.MatchesList.RemoveAll(u => u.UserId == userId || u.Id == userId);
                        removedCount += beforeCount - otherProfile.MatchesList.Count;
                    }

                    // LikedUsersList'ten çıkar (ben onları like'lamışsam)
                    if (otherProfile.ReceivedLikesList != null)
                    {
                        var beforeCount = otherProfile.ReceivedLikesList.Count;
                        otherProfile.ReceivedLikesList.RemoveAll(u => u.UserId == userId || u.Id == userId);
                        removedCount += beforeCount - otherProfile.ReceivedLikesList.Count;
                    }

                    // ReceivedLikesList'ten çıkar (onlar beni like'lamışsa)
                    if (otherProfile.LikedUsersList != null)
                    {
                        var beforeCount = otherProfile.LikedUsersList.Count;
                        otherProfile.LikedUsersList.RemoveAll(u => u.UserId == userId || u.Id == userId);
                        removedCount += beforeCount - otherProfile.LikedUsersList.Count;
                    }

                    // PassedUsersList'ten çıkar
                    if (otherProfile.PassedUsersList != null)
                    {
                        var beforeCount = otherProfile.PassedUsersList.Count;
                        otherProfile.PassedUsersList.RemoveAll(u => u.UserId == userId || u.Id == userId);
                        removedCount += beforeCount - otherProfile.PassedUsersList.Count;
                    }

                    // BlockedUsersList'ten çıkar
                    if (otherProfile.BlockedUsersList != null)
                    {
                        var beforeCount = otherProfile.BlockedUsersList.Count;
                        otherProfile.BlockedUsersList.RemoveAll(u => u.UserId == userId || u.Id == userId);
                        removedCount += beforeCount - otherProfile.BlockedUsersList.Count;
                    }

                    // ReportedUsersList'ten çıkar
                    if (otherProfile.ReportedUsersList != null)
                    {
                        var beforeCount = otherProfile.ReportedUsersList.Count;
                        otherProfile.ReportedUsersList.RemoveAll(u => u.UserId == userId || u.Id == userId);
                        removedCount += beforeCount - otherProfile.ReportedUsersList.Count;
                    }

                    if (removedCount > 0)
                    {
                        otherProfile.UpdatedAt = DateTime.UtcNow;
                        cleanedProfileCount++;
                    }
                }

                if (cleanedProfileCount > 0)
                {
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("{Count} profile'dan kullanıcı bilgileri temizlendi", cleanedProfileCount);
                }

                // ===== 3. MESAJLARI SİL (Eğer Message entity'si varsa) =====
                

                // ===== 4. REPORTS (Şikayetler) =====
                

                // Bu kullanıcı hakkında yapılan şikayetler
                

                // Bu kullanıcının yaptığı şikayetler
                

                // ===== 5. BLOCKS (Engellemeler) =====
                

                // ===== 7. SUBSCRIPTIONS (Abonelikler) =====
                

                // ===== 9. USER ACTIVITIES (Analytics) =====
                

                // ===== 10. SCREENSHOT EVENTS =====
                

                // ===== 11. VERIFICATION LOGS =====
                

                // ===== 12. EVENT PARTICIPATIONS (Eğer Event sistemi varsa) =====
                

                

                // ===== 13. EVENTS (Kullanıcının oluşturduğu eventler) =====
                

                

                // ===== 14. PROFILE'I SİL =====
                _db.ChangeTracker.Clear();

                if (profile != null)
                {
                    _db.UserProfiles.Remove(profile);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("UserProfile silindi");
                }

                // ===== 15. APPLICATION USER'I SİL =====
                _db.ChangeTracker.Clear();

                var result = await _userManager.DeleteAsync(user);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new Exception($"Kullanıcı silinemedi: {errors}");
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "✅ KULLANICI BAŞARIYLA SİLİNDİ: UserId={UserId}, Email={Email}, Silinen Fotoğraf={PhotoCount}",
                    userId, user.Email, totalPhotosDeleted);

                return "Hesabınız ve tüm verileriniz başarıyla silindi";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ DeleteUser hatası: {UserId}", userId);

                var innerEx = ex.InnerException;
                int level = 1;
                while (innerEx != null)
                {
                    _logger.LogError("Inner Exception {Level}: {Message}", level, innerEx.Message);
                    innerEx = innerEx.InnerException;
                    level++;
                }

                throw new Exception($"Hesap silme hatası: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        public async Task<string> ForgotPassword(ForgotPasswordRequestDTO forgotPasswordRequest)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == forgotPasswordRequest.Email);
                if (user == null)
                {
                    // Güvenlik için kullanıcı bulunamasa bile başarılı mesaj döndür
                    return "Eğer email adresiniz sistemde kayıtlıysa, şifre sıfırlama kodu gönderilecektir.";
                }

                // 6 haneli kod oluştur
                var resetCode = await _passwordResetCodeService.GenerateResetCodeAsync(user.Email);

                // Email servisini kullanarak kodu gönder
                await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode);

                _logger.LogInformation("Şifre sıfırlama kodu oluşturuldu ve e-posta gönderildi: {Email}", user.Email);

                return "Eğer email adresiniz sistemde kayıtlıysa, şifre sıfırlama kodu gönderilecektir.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPassword işlemi sırasında hata: {Email}", forgotPasswordRequest.Email);
                throw;
            }
        }

        public async Task<UserDto> GetUserById(string userId)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    throw new Exception("Kullanıcı bulunamadı");
                }

                // --- BURAYI EKLİYORUZ: Profil bilgisini de çekmemiz lazım ---
                var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

                var userDto = _mapper.Map<UserDto>(user);

                // Null güvenliği
                userDto.Name = userDto.Name ?? "";
                userDto.Surname = userDto.Surname ?? "";
                userDto.Gender = userDto.Gender ?? "";
                userDto.Email = userDto.Email ?? "";
                userDto.PhoneNumber = userDto.PhoneNumber ?? "";

                // --- BURAYI EKLİYORUZ: Status ataması ---
                userDto.IsProfileCreated = profile != null && profile.IsProfileCompleted;

                // Eğer profil varsa ek bilgileri de doldurabilirsin (İsteğe bağlı)
                if (profile != null)
                {
                    userDto.ProfileImageUrl = profile.ProfileImageUrl;
                    userDto.DisplayName = profile.DisplayName;
                }

                return userDto;
            }
            catch (Exception ex)
            {
                throw;
            }
        }



        public async Task<bool> IsAccountExist(string email)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
                if (user == null)
                {
                    throw new Exception("Kullanıcı bulunamadı");
                }

                return true;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<LoginResponseDto> Login(LoginRequestDTO loginRequestDTO)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.Email.ToLower() == loginRequestDTO.Email.ToLower());
            bool isValid = await _userManager.CheckPasswordAsync(user, loginRequestDTO.Password);

            if (user == null || isValid == false)
            {
                return new LoginResponseDto() { User = null, Token = "" };
            }

            // Profile bilgisini çek - Include OLMADAN
            var profile = await _db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var roles = await _userManager.GetRolesAsync(user);

            // Token'ı profile ile birlikte generate et
            var token = _jwtTokenGenerator.GenerateToken(user, profile, roles);

            UserDto userDto = new()
            {
                Email = user.Email,
                Id = user.Id,
                Name = user.FirstName,
                Surname = user.LastName,
                Gender = user.Gender,
                PhoneNumber = user.PhoneNumber,

                // Profil varsa ve IsProfileCompleted true ise, IsProfileCreated true döner.
                IsProfileCreated = profile != null && profile.IsProfileCompleted
            };

            LoginResponseDto loginResponseDto = new LoginResponseDto()
            {
                User = userDto,
                Token = token,
            };

            return loginResponseDto;
        }

        public async Task<LoginResponseDto> Register(RegistrationRequestDTO registrationRequestDTO)
        {
            try
            {
                var isExistEmail = _db.ApplicationUsers.FirstOrDefault(u => u.Email == registrationRequestDTO.Email);
                if (isExistEmail != null)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = null,
                        IsSuccess = false,
                        Message = "Bu email adresi zaten kullanılıyor"
                    };
                }

                // University domain kontrolü
                var emailDomain = registrationRequestDTO.Email.Split('@').LastOrDefault();
                if (string.IsNullOrEmpty(emailDomain) || !IsValidUniversityDomain(emailDomain))
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = null,
                        IsSuccess = false,
                        Message = "Geçerli bir üniversite e-posta adresi kullanmalısınız"
                    };
                }

                // Yaş kontrolü (18+)
                var age = CalculateAge(registrationRequestDTO.DateOfBirth);
                if (age < 18)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = null,
                        IsSuccess = false,
                        Message = "18 yaşından küçükler kayıt olamaz"
                    };
                }

                // ApplicationUser oluştur
                ApplicationUser user = new()
                {
                    FirstName = registrationRequestDTO.FirstName,
                    LastName = registrationRequestDTO.LastName,
                    Email = registrationRequestDTO.Email,
                    UserName = registrationRequestDTO.Email, // Email'i username olarak kullan
                    NormalizedEmail = registrationRequestDTO.Email.ToUpper(),
                    PhoneNumber = registrationRequestDTO.PhoneNumber,
                    Gender = registrationRequestDTO.Gender,
                    DateOfBirth = registrationRequestDTO.DateOfBirth,
                    UniversityDomain = emailDomain,
                    UniversityName = GetUniversityName(emailDomain), // registrationRequestDTO.UniversityName
                    IsUniversityVerified = false, // Email verification sonrası true olacak
                    EmailVerifiedAt = null,
                    LastVerificationCheck = DateTime.UtcNow
                };


                var result = await _userManager.CreateAsync(user, registrationRequestDTO.Password);
                if (result.Succeeded)
                {
                    var userToReturn = _db.ApplicationUsers.First(u => u.Email == registrationRequestDTO.Email);

                    // Yeni kullanıcı için UserProfile oluştur
                    var newProfile = new UserProfile
                    {
                        UserId = userToReturn.Id,
                        DisplayName = $"{userToReturn.FirstName} {userToReturn.LastName[0]}.", // "Ramiz K."
                        Bio = "",

                        // Lists - Initialize empty
                        MatchesList = new List<UsersDto>(),
                        LikedUsersList = new List<UsersDto>(),
                        ReceivedLikesList = new List<UsersDto>(),
                        PassedUsersList = new List<UsersDto>(),
                        BlockedUsersList = new List<UsersDto>(),
                        ReportedUsersList = new List<UsersDto>(),
                        PhotosList = new List<Photo>(),

                        // Profile settings - defaults
                        InterestedIn = "Everyone",
                        AgeRangeMin = 18,
                        AgeRangeMax = 30,
                        MaxDistance = 50,

                        // Privacy settings - defaults
                        ShowMyUniversity = true,
                        ShowMeOnApp = true,
                        ShowDistance = true,
                        ShowAge = true,

                        // Stats - initial values
                        ProfileCompletionScore = CalculateInitialCompletionScore(userToReturn),
                        DailySwipeCount = 0,
                        SwipeCountResetAt = DateTime.UtcNow.Date,
                        SuperLikeCount = 1, // Free users günde 1 super like
                        TotalMatchCount = 0,
                        TotalLikesReceived = 0,

                        // Premium - initial values
                        IsPremium = false,
                        PremiumExpiresAt = null,
                        UnlimitedSwipes = 0,
                        BoostsRemaining = 0,

                        // Activity stats
                        MessagesSent = 0,
                        MessagesReceived = 0,
                        ResponseRate = 0,

                        // Verification
                        IsPhotoVerified = false,
                        PhotoVerifiedAt = null,
                        FaceId = null,

                        // Timestamps
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastActiveAt = DateTime.UtcNow,
                        IsActive = true,
                        IsProfileCompleted = false
                    };

                    _db.UserProfiles.Add(newProfile);
                    await _db.SaveChangesAsync();

                    // Default role ata
                    //await _userManager.AddToRoleAsync(userToReturn, "USER");

                    var verificationCode = await _emailVerificationCodeService.GenerateVerificationCodeAsync(userToReturn.Email);
                    await _emailService.SendEmailVerificationCodeAsync(
                        userToReturn.Email,
                        verificationCode,
                        $"{userToReturn.FirstName} {userToReturn.LastName}"
                    );


                    // AutoMapper kullanın
                    UserDto userDto = _mapper.Map<UserDto>(userToReturn);

                    // Null güvenliği
                    userDto.Name = userDto.Name ?? "";
                    userDto.Surname = userDto.Surname ?? "";
                    userDto.Gender = userDto.Gender ?? "";
                    userDto.Email = userDto.Email ?? "";
                    userDto.PhoneNumber = userDto.PhoneNumber ?? "";
                    userDto.DisplayName = newProfile.DisplayName;
                    userDto.ProfileImageUrl = newProfile.ProfileImageUrl;
                    userDto.Age = age;
                    userDto.UniversityName = userToReturn.UniversityName;
                    userDto.IsVerified = userToReturn.IsUniversityVerified;

                    // Yeni kayıt olan kullanıcının profili henüz tamamlanmamıştır.
                    userDto.IsProfileCreated = false;

                    // Token generate et
                    var roles = await _userManager.GetRolesAsync(userToReturn);
                    var token = _jwtTokenGenerator.GenerateToken(userToReturn, newProfile, roles);

                    return new LoginResponseDto
                    {
                        User = userDto,
                        Token = token,
                        IsSuccess = true,
                        Message = "Kayıt başarılı! Lütfen email adresinizi doğrulayın."
                    };
                }
                else
                {
                    throw new Exception(result.Errors.FirstOrDefault()?.Description);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Helper Methods
        private bool IsValidUniversityDomain(string domain)
        {
            var validDomains = new List<string>
    {
        "bilgiedu.net",
        "boun.edu.tr",
        "itu.edu.tr",
        "metu.edu.tr",
        "sabanciuniv.edu",
        "ku.edu.tr",
        "ogu.edu.tr",
        "deu.edu.tr",
        "ege.edu.tr",
        "hacettepe.edu.tr",
        "ankara.edu.tr",
        "yildiz.edu.tr",
        "ogr.halic.edu.tr"
        // Daha fazla üniversite eklenebilir
    };

            return validDomains.Contains(domain.ToLower());
        }

        private string GetUniversityName(string domain)
        {
            var universityMap = new Dictionary<string, string>
    {
        { "bilgiedu.net", "İstanbul Bilgi Üniversitesi" },
        { "boun.edu.tr", "Boğaziçi Üniversitesi" },
        { "itu.edu.tr", "İstanbul Teknik Üniversitesi" },
        { "metu.edu.tr", "Orta Doğu Teknik Üniversitesi" },
        { "sabanciuniv.edu", "Sabancı Üniversitesi" },
        { "ku.edu.tr", "Koç Üniversitesi" },
        { "ogu.edu.tr", "Osmangazi Üniversitesi" },
        { "deu.edu.tr", "Dokuz Eylül Üniversitesi" },
        { "ege.edu.tr", "Ege Üniversitesi" },
        { "hacettepe.edu.tr", "Hacettepe Üniversitesi" },
        { "ankara.edu.tr", "Ankara Üniversitesi" },
        { "yildiz.edu.tr", "Yıldız Teknik Üniversitesi" },
        {"ogr.halic.edu.tr", "Halic Universitesi" }
    };

            return universityMap.TryGetValue(domain.ToLower(), out var name) ? name : domain;
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        private int CalculateInitialCompletionScore(ApplicationUser user)
        {
            int score = 0;

            // Basic info (40%)
            if (!string.IsNullOrEmpty(user.FirstName)) score += 10;
            if (!string.IsNullOrEmpty(user.LastName)) score += 10;
            if (!string.IsNullOrEmpty(user.Email)) score += 10;
            if (!string.IsNullOrEmpty(user.Gender)) score += 10;

            // Email verification (20%)
            // Will be added when email is verified

            // Profile completion items (40%) - will be added later:
            // - Bio (10%)
            // - Photos (20%)
            // - Interests (10%)

            return score;
        }

        public async Task<string> ResetPasswordWithCode(ResetPasswordWithCodeRequestDTO resetPasswordRequest)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == resetPasswordRequest.Email);
                if (user == null)
                {
                    throw new Exception("Geçersiz email adresi");
                }

                // Önce bu email için kod var mı kontrol et
                var cacheKey = $"reset_code_{resetPasswordRequest.Email}";
                if (!_cache.TryGetValue(cacheKey, out string cachedCode))
                {
                    throw new Exception("Bu email adresi için aktif bir şifre sıfırlama kodu bulunamadı. Lütfen yeni bir kod talep edin.");
                }

                // Kodu doğrula
                var isValidCode = await _passwordResetCodeService.ValidateResetCodeAsync(resetPasswordRequest.Email, resetPasswordRequest.ResetCode);
                if (!isValidCode)
                {
                    throw new Exception("Girilen kod hatalı");
                }

                // Şifreyi sıfırla
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, resetToken, resetPasswordRequest.NewPassword);

                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.FirstOrDefault()?.Description);
                }

                // Kodu geçersiz kıl
                await _passwordResetCodeService.InvalidateResetCodeAsync(resetPasswordRequest.Email);

                return "Şifreniz başarıyla sıfırlandı";
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<UserDto> UpdateUser(UpdateUserRequestDTO updateRequest)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == updateRequest.UserId);
                if (user == null)
                {
                    throw new Exception("Kullanıcı bulunamadı");
                }

                // Güncelleme işlemi
                if (!string.IsNullOrEmpty(updateRequest.Name))
                    user.FirstName = updateRequest.Name;

                if (!string.IsNullOrEmpty(updateRequest.Surname))
                    user.LastName = updateRequest.Surname;

                if (!string.IsNullOrEmpty(updateRequest.Gender))
                    user.Gender = updateRequest.Gender;

                if (!string.IsNullOrEmpty(updateRequest.Email))
                {
                    user.Email = updateRequest.Email;
                    user.NormalizedEmail = updateRequest.Email.ToUpper();
                }

                if (!string.IsNullOrEmpty(updateRequest.PhoneNumber))
                    user.PhoneNumber = updateRequest.PhoneNumber;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.FirstOrDefault()?.Description);
                }

                var userDto = _mapper.Map<UserDto>(user);

                // Null güvenliği
                userDto.Name = userDto.Name ?? "";
                userDto.Surname = userDto.Surname ?? "";
                userDto.Gender = userDto.Gender ?? "";
                userDto.Email = userDto.Email ?? "";
                userDto.PhoneNumber = userDto.PhoneNumber ?? "";

                return userDto;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public async Task<string> VerifyEmailWithCode(VerifyEmailWithCodeRequestDTO verifyRequest)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == verifyRequest.Email);
                if (user == null)
                {
                    throw new Exception("Kullanıcı bulunamadı");
                }

                if (user.EmailConfirmed)
                {
                    return "Email adresi zaten doğrulanmış";
                }

                // Kodu doğrula
                var isValidCode = await _emailVerificationCodeService.ValidateVerificationCodeAsync(
                    verifyRequest.Email,
                    verifyRequest.VerificationCode
                );

                if (!isValidCode)
                {
                    throw new Exception("Girilen kod hatalı veya süresi dolmuş");
                }

                // Email'i doğrula
                user.EmailConfirmed = true;
                user.EmailVerifiedAt = DateTime.UtcNow;
                user.IsUniversityVerified = true;

                await _userManager.UpdateAsync(user);

                // Profile completion score'u güncelle
                var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (profile != null)
                {
                    profile.ProfileCompletionScore += 20; // Email verification için 20 puan
                    profile.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }

                // Kodu geçersiz kıl
                await _emailVerificationCodeService.InvalidateVerificationCodeAsync(verifyRequest.Email);

                _logger.LogInformation("Email başarıyla doğrulandı: {Email}", verifyRequest.Email);
                return "Email adresiniz başarıyla doğrulandı! Artık tüm özellikleri kullanabilirsiniz.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama hatası: {Email}", verifyRequest.Email);
                throw;
            }
        }

        public async Task<string> ResendVerificationCode(ResendVerificationCodeRequestDTO resendRequest)
        {
            try
            {
                var user = await _db.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == resendRequest.Email);
                if (user == null)
                {
                    return "Eğer email adresiniz sistemde kayıtlıysa, doğrulama kodu gönderilecektir.";
                }

                if (user.EmailConfirmed)
                {
                    return "Email adresi zaten doğrulanmış";
                }

                // Yeni kod oluştur ve gönder
                var verificationCode = await _emailVerificationCodeService.GenerateVerificationCodeAsync(user.Email);
                await _emailService.SendEmailVerificationCodeAsync(
                    user.Email,
                    verificationCode,
                    $"{user.FirstName} {user.LastName}"
                );

                _logger.LogInformation("Yeni doğrulama kodu gönderildi: {Email}", user.Email);
                return "Yeni doğrulama kodu email adresinize gönderildi.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Doğrulama kodu tekrar gönderilirken hata: {Email}", resendRequest.Email);
                throw;
            }
        }
    }
}
