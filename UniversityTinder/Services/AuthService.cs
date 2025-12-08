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
using static Utility.SD;

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
                        InterestedIn = InterestedInType.Everyone, // "Everyone" yerine enum
                        AgeRangeMin = 18,
                        AgeRangeMax = 30,
                        MaxDistance = 50,

                        Hobbies = new List<Hobbies>(),

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
            // Dictionary'de anahtarın bulunup bulunmadığını kontrol eder.
            return _universityDomainMap.ContainsKey(domain.ToLower());
        }

        private static readonly Dictionary<string, string> _universityDomainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"agu.edu.tr", "ABDULLAH GÜL ÜNİVERSİTESİ"},
        {"acibadem.edu.tr", "ACIBADEM MEHMET ALİ AYDINLAR ÜNİVERSİTESİ"},
        {"atu.edu.tr", "ADANA ALPARSLAN TÜRKEŞ BİLİM VE TEKNOLOJİ ÜNİVERSİTESİ"},
        {"adiyaman.edu.tr", "ADIYAMAN ÜNİVERSİTESİ"},
        {"aku.edu.tr", "AFYON KOCATEPE ÜNİVERSİTESİ"},
        {"afsu.edu.tr", "AFYONKARAHİSAR SAĞLIK BİLİMLERİ ÜNİVERSİTESİ"},
        {"agri.edu.tr", "AĞRI İBRAHİM ÇEÇEN ÜNİVERSİTESİ"},
        {"akdeniz.edu.tr", "AKDENİZ ÜNİVERSİTESİ"},
        {"aksaray.edu.tr", "AKSARAY ÜNİVERSİTESİ"},
        {"alanya.edu.tr", "ALANYA ALAADDİN KEYKUBAT ÜNİVERSİTESİ"},
        {"alanyauniversity.edu.tr", "ALANYA ÜNİVERSİTESİ"},
        {"altinbas.edu.tr", "ALTINBAŞ ÜNİVERSİTESİ"},
        {"amasya.edu.tr", "AMASYA ÜNİVERSİTESİ"},
        {"anadolu.edu.tr", "ANADOLU ÜNİVERSİTESİ"},
        {"ankarabilim.edu.tr", "ANKARA BİLİM ÜNİVERSİTESİ"},
        {"hacibayram.edu.tr", "ANKARA HACI BAYRAM VELİ ÜNİVERSİTESİ"},
        {"ankaramedipol.edu.tr", "ANKARA MEDİPOL ÜNİVERSİTESİ"},
        {"mgu.edu.tr", "ANKARA MÜZİK VE GÜZEL SANATLAR ÜNİVERSİTESİ"},
        {"asbu.edu.tr", "ANKARA SOSYAL BİLİMLER ÜNİVERSİTESİ"},
        {"ankara.edu.tr", "ANKARA ÜNİVERSİTESİ"},
        {"aybu.edu.tr", "ANKARA YILDIRIM BEYAZIT ÜNİVERSİTESİ"},
        {"belek.edu.tr", "ANTALYA BELEK ÜNİVERSİTESİ"},
        {"antalya.edu.tr", "ANTALYA BİLİM ÜNİVERSİTESİ"},
        {"ardahan.edu.tr", "ARDAHAN ÜNİVERSİTESİ"},
        {"artvin.edu.tr", "ARTVİN ÇORUH ÜNİVERSİTESİ"},
        {"adiguzel.edu.tr", "ATAŞEHİR ADIGÜZEL MESLEK YÜKSEKOKULU"},
        {"atauni.edu.tr", "ATATÜRK ÜNİVERSİTESİ"},
        {"atilim.edu.tr", "ATILIM ÜNİVERSİTESİ"},
        {"avrasya.edu.tr", "AVRASYA ÜNİVERSİTESİ"},
        {"adu.edu.tr", "AYDIN ADNAN MENDERES ÜNİVERSİTESİ"},
        {"bahcesehir.edu.tr", "BAHÇEŞEHİR ÜNİVERSİTESİ"},
        {"balikesir.edu.tr", "BALIKESİR ÜNİVERSİTESİ"},
        {"bandirma.edu.tr", "BANDIRMA ONYEDİ EYLÜL ÜNİVERSİTESİ"},
        {"bartin.edu.tr", "BARTIN ÜNİVERSİTESİ"},
        {"baskent.edu.tr", "BAŞKENT ÜNİVERSİTESİ"},
        {"batman.edu.tr", "BATMAN ÜNİVERSİTESİ"},
        {"bayburt.edu.tr", "BAYBURT ÜNİVERSİTESİ"},
        {"beykoz.edu.tr", "BEYKOZ ÜNİVERSİTESİ"},
        {"bezmialem.edu.tr", "BEZM-İ ÂLEM VAKIF ÜNİVERSİTESİ"},
        {"bilecik.edu.tr", "BİLECİK ŞEYH EDEBALİ ÜNİVERSİTESİ"},
        {"bingol.edu.tr", "BİNGÖL ÜNİVERSİTESİ"},
        {"biruni.edu.tr", "BİRUNİ ÜNİVERSİTESİ"},
        {"beu.edu.tr", "BİTLİS EREN ÜNİVERSİTESİ"},
        {"boun.edu.tr", "BOĞAZİÇİ ÜNİVERSİTESİ"},
        {"ibu.edu.tr", "BOLU ABANT İZZET BAYSAL ÜNİVERSİTESİ"},
        {"mehmetakif.edu.tr", "BURDUR MEHMET AKİF ERSOY ÜNİVERSİTESİ"},
        {"btu.edu.tr", "BURSA TEKNİK ÜNİVERSİTESİ"},
        {"uludag.edu.tr", "BURSA ULUDAĞ ÜNİVERSİTESİ"},
        {"cag.edu.tr", "ÇAĞ ÜNİVERSİTESİ"},
        {"comu.edu.tr", "ÇANAKKALE ONSEKİZ MART ÜNİVERSİTESİ"},
        {"cankaya.edu.tr", "ÇANKAYA ÜNİVERSİTESİ"},
        {"karatekin.edu.tr", "ÇANKIRI KARATEKİN ÜNİVERSİTESİ"},
        {"cu.edu.tr", "ÇUKUROVA ÜNİVERSİTESİ"},
        {"demiroglu.bilim.edu.tr", "DEMİROĞLU BİLİM ÜNİVERSİTESİ"},
        {"dicle.edu.tr", "DİCLE ÜNİVERSİTESİ"},
        {"dogus.edu.tr", "DOĞUŞ ÜNİVERSİTESİ"},
        {"deu.edu.tr", "DOKUZ EYLÜL ÜNİVERSİTESİ"},
        {"duzce.edu.tr", "DÜZCE ÜNİVERSİTESİ"},
        {"ege.edu.tr", "EGE ÜNİVERSİTESİ"},
        {"erciyes.edu.tr", "ERCİYES ÜNİVERSİTESİ"},
        {"erzincan.edu.tr", "ERZİNCAN BİNALİ YILDIRIM ÜNİVERSİTESİ"},
        {"erzurum.edu.tr", "ERZURUM TEKNİK ÜNİVERSİTESİ"},
        {"ogu.edu.tr", "ESKİŞEHİR OSMANGAZİ ÜNİVERSİTESİ"},
        {"eskisehir.edu.tr", "ESKİŞEHİR TEKNİK ÜNİVERSİTESİ"},
        {"fsm.edu.tr", "FATİH SULTAN MEHMET VAKIF ÜNİVERSİTESİ"},
        {"fbu.edu.tr", "FENERBAHÇE ÜNİVERSİTESİ"},
        {"firat.edu.tr", "FIRAT ÜNİVERSİTESİ"},
        {"gsu.edu.tr", "GALATASARAY ÜNİVERSİTESİ"},
        {"gazi.edu.tr", "GAZİ ÜNİVERSİTESİ"},
        {"gibtu.edu.tr", "GAZİANTEP İSLAM BİLİM VE TEKNOLOJİ ÜNİVERSİTESİ"},
        {"gantep.edu.tr", "GAZİANTEP ÜNİVERSİTESİ"},
        {"gtu.edu.tr", "GEBZE TEKNİK ÜNİVERSİTESİ"},
        {"giresun.edu.tr", "GİRESUN ÜNİVERSİTESİ"},
        {"gumushane.edu.tr", "GÜMÜŞHANE ÜNİVERSİTESİ"},
        {"hacettepe.edu.tr", "HACETTEPE ÜNİVERSİTESİ"},
        {"hakkari.edu.tr", "HAKKARİ ÜNİVERSİTESİ"},
        {"halic.edu.tr", "HALİÇ ÜNİVERSİTESİ"},
        {"harran.edu.tr", "HARRAN ÜNİVERSİTESİ"},
        {"hku.edu.tr", "HASAN KALYONCU ÜNİVERSİTESİ"},
        {"mku.edu.tr", "HATAY MUSTAFA KEMAL ÜNİVERSİTESİ"},
        {"hitit.edu.tr", "HİTİT ÜNİVERSİTESİ"},
        {"igdir.edu.tr", "IĞDIR ÜNİVERSİTESİ"},
        {"isparta.edu.tr", "ISPARTA UYGULAMALI BİLİMLER ÜNİVERSİTESİ"},
        {"isikun.edu.tr", "IŞIK ÜNİVERSİTESİ"},
        {"ihu.edu.tr", "İBN HALDUN ÜNİVERSİTESİ"},
        {"bilkent.edu.tr", "İHSAN DOĞRAMACI BİLKENT ÜNİVERSİTESİ"},
        {"inonu.edu.tr", "İNÖNÜ ÜNİVERSİTESİ"},
        {"iste.edu.tr", "İSKENDERUN TEKNİK ÜNİVERSİTESİ"},
        {"29mayis.edu.tr", "İSTANBUL 29 MAYIS ÜNİVERSİTESİ"},
        {"arel.edu.tr", "İSTANBUL AREL ÜNİVERSİTESİ"},
        {"atlas.edu.tr", "İSTANBUL ATLAS ÜNİVERSİTESİ"},
        {"aydin.edu.tr", "İSTANBUL AYDIN ÜNİVERSİTESİ"},
        {"beykent.edu.tr", "İSTANBUL BEYKENT ÜNİVERSİTESİ"},
        {"bilgi.edu.tr", "İSTANBUL BİLGİ ÜNİVERSİTESİ"},
        {"esenyurt.edu.tr", "İSTANBUL ESENYURT ÜNİVERSİTESİ"},
        {"galata.edu.tr", "İSTANBUL GALATA ÜNİVERSİTESİ"},
        {"gedik.edu.tr", "İSTANBUL GEDİK ÜNİVERSİTESİ"},
        {"gelisim.edu.tr", "İSTANBUL GELİŞİM ÜNİVERSİTESİ"},
        {"kent.edu.tr", "İSTANBUL KENT ÜNİVERSİTESİ"},
        {"iku.edu.tr", "İSTANBUL KÜLTÜR ÜNİVERSİTESİ"},
        {"medeniyet.edu.tr", "İSTANBUL MEDENİYET ÜNİVERSİTESİ"},
        {"medipol.edu.tr", "İSTANBUL MEDİPOL ÜNİVERSİTESİ"},
        {"nisantasi.edu.tr", "İSTANBUL NİŞANTAŞI ÜNİVERSİTESİ"},
        {"okan.edu.tr", "İSTANBUL OKAN ÜNİVERSİTESİ"},
        {"rumeli.edu.tr", "İSTANBUL RUMELİ ÜNİVERSİTESİ"},
        {"izu.edu.tr", "İSTANBUL SABAHATTİN ZAİM ÜNİVERSİTESİ"},
        {"issb.edu.tr", "İSTANBUL SAĞLIK VE SOSYAL BİLİMLER MESLEK YÜKSEKOKULU"},
        {"istun.edu.tr", "İSTANBUL SAĞLIK VE TEKNOLOJİ ÜNİVERSİTESİ"},
        {"sisli.edu.tr", "İSTANBUL ŞİŞLİ MESLEK YÜKSEKOKULU"},
        {"itu.edu.tr", "İSTANBUL TEKNİK ÜNİVERSİTESİ"},
        {"ticaret.edu.tr", "İSTANBUL TİCARET ÜNİVERSİTESİ"},
        {"topkapi.edu.tr", "İSTANBUL TOPKAPI ÜNİVERSİTESİ"},
        {"istanbul.edu.tr", "İSTANBUL ÜNİVERSİTESİ"},
        {"iuc.edu.tr", "İSTANBUL ÜNİVERSİTESİ-CERRAHPAŞA"},
        {"yeniyuzyil.edu.tr", "İSTANBUL YENİ YÜZYIL ÜNİVERSİTESİ"},
        {"istinye.edu.tr", "İSTİNYE ÜNİVERSİTESİ"},
        {"bakircay.edu.tr", "İZMİR BAKIRÇAY ÜNİVERSİTESİ"},
        {"idu.edu.tr", "İZMİR DEMOKRASİ ÜNİVERSİTESİ"},
        {"ieu.edu.tr", "İZMİR EKONOMİ ÜNİVERSİTESİ"},
        {"ikc.edu.tr", "İZMİR KATİP ÇELEBİ ÜNİVERSİTESİ"},
        {"kavram.edu.tr", "İZMİR KAVRAM MESLEK YÜKSEKOKULU"},
        {"tinaztepe.edu.tr", "İZMİR TINAZTEPE ÜNİVERSİTESİ"},
        {"iyte.edu.tr", "İZMİR YÜKSEK TEKNOLOJİ ENSTİTÜSÜ"},
        {"khas.edu.tr", "KADİR HAS ÜNİVERSİTESİ"},
        {"kafkas.edu.tr", "KAFKAS ÜNİVERSİTESİ"},
        {"istiklal.edu.tr", "KAHRAMANMARAŞ İSTİKLAL ÜNİVERSİTESİ"},
        {"ksu.edu.tr", "KAHRAMANMARAŞ SÜTÇÜ İMAM ÜNİVERSİTESİ"},
        {"kapadokya.edu.tr", "KAPADOKYA ÜNİVERSİTESİ"},
        {"karabuk.edu.tr", "KARABÜK ÜNİVERSİTESİ"},
        {"ktu.edu.tr", "KARADENİZ TEKNİK ÜNİVERSİTESİ"},
        {"kmu.edu.tr", "KARAMANOĞLU MEHMETBEY ÜNİVERSİTESİ"},
        {"kastamonu.edu.tr", "KASTAMONU ÜNİVERSİTESİ"},
        {"kayseri.edu.tr", "KAYSERİ ÜNİVERSİTESİ"},
        {"kku.edu.tr", "KIRIKKALE ÜNİVERSİTESİ"},
        {"klu.edu.tr", "KIRKLARELİ ÜNİVERSİTESİ"},
        {"ahievran.edu.tr", "KIRŞEHİR AHİ EVRAN ÜNİVERSİTESİ"},
        {"kilis.edu.tr", "KİLİS 7 ARALIK ÜNİVERSİTESİ"},
        {"kocaelisaglik.edu.tr", "KOCAELİ SAĞLIK VE TEKNOLOJİ ÜNİVERSİTESİ"},
        {"kocaeli.edu.tr", "KOCAELİ ÜNİVERSİTESİ"},
        {"ku.edu.tr", "KOÇ ÜNİVERSİTESİ"},
        {"gidatarim.edu.tr", "KONYA GIDA VE TARIM ÜNİVERSİTESİ"},
        {"ktun.edu.tr", "KONYA TEKNİK ÜNİVERSİTESİ"},
        {"karatay.edu.tr", "KTO KARATAY ÜNİVERSİTESİ"},
        {"dpu.edu.tr", "KÜTAHYA DUMLUPINAR ÜNİVERSİTESİ"},
        {"ksbu.edu.tr", "KÜTAHYA SAĞLIK BİLİMLERİ ÜNİVERSİTESİ"},
        {"lokmanhekim.edu.tr", "LOKMAN HEKİM ÜNİVERSİTESİ"},
        {"ozal.edu.tr", "MALATYA TURGUT ÖZAL ÜNİVERSİTESİ"},
        {"maltepe.edu.tr", "MALTEPE ÜNİVERSİTESİ"},
        {"cbu.edu.tr", "MANİSA CELÂL BAYAR ÜNİVERSİTESİ"},
        {"artuklu.edu.tr", "MARDİN ARTUKLU ÜNİVERSİTESİ"},
        {"marmara.edu.tr", "MARMARA ÜNİVERSİTESİ"},
        {"mef.edu.tr", "MEF ÜNİVERSİTESİ"},
        {"mersin.edu.tr", "MERSİN ÜNİVERSİTESİ"},
        {"msgsu.edu.tr", "MİMAR SİNAN GÜZEL SANATLAR ÜNİVERSİTESİ"},
        {"mudanya.edu.tr", "MUDANYA ÜNİVERSİTESİ"},
        {"mu.edu.tr", "MUĞLA SITKI KOÇMAN ÜNİVERSİTESİ"},
        {"munzur.edu.tr", "MUNZUR ÜNİVERSİTESİ"},
        {"alparslan.edu.tr", "MUŞ ALPARSLAN ÜNİVERSİTESİ"},
        {"erbakan.edu.tr", "NECMETTİN ERBAKAN ÜNİVERSİTESİ"},
        {"nevsehir.edu.tr", "NEVŞEHİR HACI BEKTAŞ VELİ ÜNİVERSİTESİ"},
        {"ohu.edu.tr", "NİĞDE ÖMER HALİSDEMİR ÜNİVERSİTESİ"},
        {"nny.edu.tr", "NUH NACİ YAZGAN ÜNİVERSİTESİ"},
        {"omu.edu.tr", "ONDOKUZ MAYIS ÜNİVERSİTESİ"},
        {"odu.edu.tr", "ORDU ÜNİVERSİTESİ"},
        {"metu.edu.tr", "ORTA DOĞU TEKNİK ÜNİVERSİTESİ"},
        {"osmaniye.edu.tr", "OSMANİYE KORKUT ATA ÜNİVERSİTESİ"},
        {"ostimteknik.edu.tr", "OSTİM TEKNİK ÜNİVERSİTESİ"},
        {"ozyegin.edu.tr", "ÖZYEĞİN ÜNİVERSİTESİ"},
        {"pau.edu.tr", "PAMUKKALE ÜNİVERSİTESİ"},
        {"pirireis.edu.tr", "PİRİ REİS ÜNİVERSİTESİ"},
        {"erdogan.edu.tr", "RECEP TAYYİP ERDOĞAN ÜNİVERSİTESİ"},
        {"sabanciuniv.edu", "SABANCI ÜNİVERSİTESİ"},
        {"sbu.edu.tr", "SAĞLIK BİLİMLERİ ÜNİVERSİTESİ"},
        {"subu.edu.tr", "SAKARYA UYGULAMALI BİLİMLER ÜNİVERSİTESİ"},
        {"sakarya.edu.tr", "SAKARYA ÜNİVERSİTESİ"},
        {"samsun.edu.tr", "SAMSUN ÜNİVERSİTESİ"},
        {"sanko.edu.tr", "SANKO ÜNİVERSİTESİ"},
        {"selcuk.edu.tr", "SELÇUK ÜNİVERSİTESİ"},
        {"siirt.edu.tr", "SİİRT ÜNİVERSİTESİ"},
        {"sinop.edu.tr", "SİNOP ÜNİVERSİTESİ"},
        {"sivas.edu.tr", "SİVAS BİLİM VE TEKNOLOJİ ÜNİVERSİTESİ"},
        {"cumhuriyet.edu.tr", "SİVAS CUMHURİYET ÜNİVERSİTESİ"},
        {"sdu.edu.tr", "SÜLEYMAN DEMİREL ÜNİVERSİTESİ"},
        {"sirnak.edu.tr", "ŞIRNAK ÜNİVERSİTESİ"},
        {"tarsus.edu.tr", "TARSUS ÜNİVERSİTESİ"},
        {"tedu.edu.tr", "TED ÜNİVERSİTESİ"},
        {"nku.edu.tr", "TEKİRDAĞ NAMIK KEMAL ÜNİVERSİTESİ"},
        {"etu.edu.tr", "TOBB EKONOMİ VE TEKNOLOJİ ÜNİVERSİTESİ"},
        {"gop.edu.tr", "TOKAT GAZİOSMANPAŞA ÜNİVERSİTESİ"},
        {"toros.edu.tr", "TOROS ÜNİVERSİTESİ"},
        {"trabzon.edu.tr", "TRABZON ÜNİVERSİTESİ"},
        {"trakya.edu.tr", "TRAKYA ÜNİVERSİTESİ"},
        {"thk.edu.tr", "TÜRK HAVA KURUMU ÜNİVERSİTESİ"},
        {"tau.edu.tr", "TÜRK-ALMAN ÜNİVERSİTESİ"},
        {"ufuk.edu.tr", "UFUK ÜNİVERSİTESİ"},
        {"usak.edu.tr", "UŞAK ÜNİVERSİTESİ"},
        {"uskudar.edu.tr", "ÜSKÜDAR ÜNİVERSİTESİ"},
        {"yyu.edu.tr", "VAN YÜZÜNCÜ YIL ÜNİVERSİTESİ"},
        {"yalova.edu.tr", "YALOVA ÜNİVERSİTESİ"},
        {"yasar.edu.tr", "YAŞAR ÜNİVERSİTESİ"},
        {"yeditepe.edu.tr", "YEDİTEPE ÜNİVERSİTESİ"},
        {"yildiz.edu.tr", "YILDIZ TEKNİK ÜNİVERSİTESİ"},
        {"bozok.edu.tr", "YOZGAT BOZOK ÜNİVERSİTESİ"},
        // Hatalı domaini düzelttik:
        {"hiu.edu.tr", "YÜKSEK İHTİSAS ÜNİVERSİTESİ"},
        {"beun.edu.tr", "ZONGULDAK BÜLENT ECEVİT ÜNİVERSİTESİ"},
    };


        private string GetUniversityName(string domain)
        {
            // Dictionary'den değeri almaya çalışır. Bulamazsa, domainin kendisini döndürür.
            if (_universityDomainMap.TryGetValue(domain.ToLower(), out var name))
            {
                return name;
            }
            // Bu, normalde olmamalıdır çünkü IsValidUniversityDomain kontrolü önceden yapılmış olmalıdır.
            return domain;
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
            score += 10;

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

                if (updateRequest.Gender.HasValue)
                    user.Gender = updateRequest.Gender.Value;

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
