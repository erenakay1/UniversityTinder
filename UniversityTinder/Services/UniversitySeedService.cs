using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UniversityTinder.Data;
using UniversityTinder.Models;
using UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;
using static Utility.SD;

namespace UniversityTinder.Services
{
    public class UniversitySeedService : IUniversitySeedService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UniversitySeedService> _logger;

        // Türkiye'deki başlıca üniversiteler
        private static readonly List<(string Name, string Domain)> Universities = new()
        {
            ("Bilgi Üniversitesi", "bilgiedu.net"),
            ("Boğaziçi Üniversitesi", "boun.edu.tr"),
            ("İstanbul Teknik Üniversitesi", "itu.edu.tr"),
            ("Koç Üniversitesi", "ku.edu.tr"),
            ("Sabancı Üniversitesi", "sabanciuniv.edu"),
            ("ODTÜ", "metu.edu.tr"),
            ("Bilkent Üniversitesi", "bilkent.edu.tr"),
            ("Hacettepe Üniversitesi", "hacettepe.edu.tr"),
            ("Ankara Üniversitesi", "ankara.edu.tr"),
            ("İstanbul Üniversitesi", "istanbul.edu.tr"),
            ("Yıldız Teknik Üniversitesi", "yildiz.edu.tr"),
            ("Marmara Üniversitesi", "marmara.edu.tr"),
            ("Galatasaray Üniversitesi", "gsu.edu.tr"),
            ("Bahçeşehir Üniversitesi", "bau.edu.tr"),
            ("Özyeğin Üniversitesi", "ozyegin.edu.tr")
        };

        // İstanbul'daki ilçeler
        private static readonly List<(string District, double Lat, double Lng)> IstanbulDistricts = new()
        {
            ("Beşiktaş", 41.0422, 29.0097),
            ("Kadıköy", 40.9903, 29.0259),
            ("Şişli", 41.0602, 28.9866),
            ("Beyoğlu", 41.0370, 28.9784),
            ("Üsküdar", 41.0256, 29.0088),
            ("Sarıyer", 41.1623, 29.0433),
            ("Bakırköy", 40.9799, 28.8738),
            ("Maltepe", 40.9280, 29.1267),
            ("Ataşehir", 40.9827, 29.1237),
            ("Beykoz", 41.1344, 29.0987),
            ("Kartal", 40.9027, 29.1816),
            ("Pendik", 40.8784, 29.2333),
            ("Tuzla", 40.8257, 29.2972),
            ("Kağıthane", 41.0785, 28.9746),
            ("Eyüpsultan", 41.0465, 28.9342)
        };

        // Ankara'daki ilçeler
        private static readonly List<(string District, double Lat, double Lng)> AnkaraDistricts = new()
        {
            ("Çankaya", 39.9179, 32.8644),
            ("Keçiören", 39.9689, 32.8596),
            ("Yenimahalle", 39.9733, 32.7481),
            ("Mamak", 39.9167, 32.9167),
            ("Etimesgut", 39.9186, 32.6775),
            ("Altındağ", 39.9463, 32.8678),
            ("Pursaklar", 40.0351, 32.9045),
            ("Sincan", 39.9681, 32.5781)
        };

        // Bölümler
        private static readonly List<string> Departments = new()
        {
            "Bilgisayar Mühendisliği",
            "Yazılım Mühendisliği",
            "Elektrik-Elektronik Mühendisliği",
            "Endüstri Mühendisliği",
            "Makine Mühendisliği",
            "İnşaat Mühendisliği",
            "Kimya Mühendisliği",
            "İşletme",
            "Ekonomi",
            "Uluslararası İlişkiler",
            "Hukuk",
            "Tıp",
            "Diş Hekimliği",
            "Mimarlık",
            "İç Mimarlık",
            "Psikoloji",
            "Sosyoloji",
            "İletişim",
            "Radyo TV Sinema",
            "Grafik Tasarım",
            "Müzik",
            "İngiliz Dili ve Edebiyatı",
            "Matematik",
            "Fizik",
            "Biyoloji",
            "Moleküler Biyoloji",
            "Tarih",
            "Felsefe",
            "Siyaset Bilimi"
        };

        // İsimler (Türkçe)
        private static readonly List<string> MaleNames = new()
        {
            "Ahmet", "Mehmet", "Mustafa", "Ali", "Hüseyin", "İbrahim", "Hasan", "Yusuf",
            "Burak", "Cem", "Deniz", "Efe", "Emre", "Eren", "Furkan", "Kerem", "Mert",
            "Oğuz", "Onur", "Serkan", "Tolga", "Utku", "Volkan", "Yasin", "Berk",
            "Barış", "Arda", "Kaan", "Koray", "Tuna", "Çağlar", "Doruk", "Eray",
            "Gökhan", "Berkay", "Alp", "Can", "Taylan", "Umut", "Yiğit"
        };

        private static readonly List<string> FemaleNames = new()
        {
            "Ayşe", "Fatma", "Zeynep", "Elif", "Emine", "Merve", "Selin", "Ebru",
            "Aslı", "Büşra", "Cemre", "Defne", "Ece", "Gizem", "İpek", "Melis",
            "Nehir", "Özge", "Pınar", "Seda", "Tuğçe", "Yasemin", "Beste", "Ceren",
            "Duygu", "Esra", "Fulya", "Gamze", "Hilal", "İrem", "Begüm", "Damla",
            "Ezgi", "Gül", "Nur", "Su", "Ela", "Ada", "Lara", "Nil"
        };

        private static readonly List<string> Surnames = new()
        {
            "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Yıldırım", "Öztürk",
            "Aydın", "Özdemir", "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Kara",
            "Koç", "Kurt", "Özkan", "Şimşek", "Erdoğan", "Polat", "Güneş", "Avcı",
            "Türk", "Akın", "Güler", "Yavuz", "Demirci", "Sarı", "Aksoy", "Deniz",
            "Bulut", "Özer", "Korkmaz", "Tekin", "Yaman", "Turan", "Eren", "Kaplan"
        };

        // Bio şablonları
        private static readonly List<string> BioTemplates = new()
        {
            "Kahve tutkunu ☕ | Gün batımı fotoğrafçısı 📸",
            "Kitap kurdu 📚 | Yeni yerler keşfetmeyi seviyorum 🌍",
            "Müzik her şeyim 🎵 | Festival aşığı 🎪",
            "Spor salonunda buluşalım 💪 | Sağlıklı yaşam 🥗",
            "Sinema delisi 🎬 | Marvel hayranı 🦸",
            "Deniz, kumsal, huzur 🌊 | Seyahat tutkunu ✈️",
            "Yoga & meditasyon 🧘 | Pozitif enerji ✨",
            "Kedilerle aramız iyi 🐱 | Doğa yürüyüşleri 🏔️",
            "Kod yazmak benim işim 💻 | Kahve içmek hobim ☕",
            "Sanat galerilerinde kaybolmak 🎨 | Minimalist 🌿",
            "Bisiklet gezileri 🚴 | Makarna uzmanı 🍝",
            "Gaming geceleri 🎮 | Pizza is life 🍕",
            "Yeni kültürler öğreniyorum 🗺️ | Dil öğrenmek eğlenceli 🗣️",
            "Gitar çalıyorum 🎸 | Konserler benim tutkum 🎤",
            "Startup hayatı 🚀 | Girişimcilik ruhu 💡",
            "Minimalist yaşam 🌱 | Sürdürülebilirlik ♻️",
            "Boks & kickbox 🥊 | Disiplin = özgürlük 💪",
            "Dans etmeyi seviyorum 💃 | Hayat bir pist 🕺",
            "Teknoloji meraklısı 📱 | AI & ML öğreniyorum 🤖",
            "Fotoğraf çekmeyi seviyorum 📷 | Anları yakalıyorum ⏱️",
            "Voleybol oynuyorum 🏐 | Takım ruhu 💯",
            "Anime & manga fan 🍜 | Cosplay yapıyorum 👘",
            "Podcast dinlemeyi severim 🎧 | Kendimi geliştiriyorum 📈",
            "Yemek yapmayı seviyorum 👨‍🍳 | Instagram: @myfood 🍰",
            "Rock müzik 🎸 | Konser kaçağıyım 🤘",
            "Sakin kafeler ☕ | Kaliteli sohbet 💬",
            "Hayvanları seviyorum 🐶 | Gönüllü çalışma 🤝",
            "E-spor oyuncusu 🎮 | Challenger tier 🏆",
            "Tiyatro & stand-up 🎭 | Gülmek önemli 😄",
            "Outdoor aktiviteleri 🏕️ | Kamp hayatı 🔥"
        };

        public UniversitySeedService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<UniversitySeedService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<bool> HasDataAsync()
        {
            return await _context.UserProfiles.AnyAsync();
        }

        public async Task<SeedStatsDto> GetSeedStatsAsync()
        {
            // ⭐ Include'ları kaldır - [NotMapped] listeler otomatik yüklenir
            var profiles = await _context.UserProfiles
                .Include(p => p.User)
                .Include(p => p.PhotosList)  // Sadece gerçek navigation property'ler
                .ToListAsync();

            var stats = new SeedStatsDto
            {
                TotalUsers = profiles.Count,
                MaleUsers = profiles.Count(p => p.User.Gender == GenderType.Male),
                FemaleUsers = profiles.Count(p => p.User.Gender == GenderType.Female),
                PremiumUsers = profiles.Count(p => p.IsPremium),
                FreeUsers = profiles.Count(p => !p.IsPremium),
                VerifiedUsers = profiles.Count(p => p.IsPhotoVerified),
                TotalProfiles = profiles.Count,
                TotalPhotos = profiles.Sum(p => p.PhotosList?.Count ?? 0),

                // ⭐ Listeler artık otomatik deserialize edilir (JSON'dan)
                TotalLikes = profiles.Sum(p => p.LikedUsersList?.Count ?? 0),
                TotalMatches = profiles.Sum(p => p.MatchesList?.Count ?? 0),
                TotalPasses = profiles.Sum(p => p.PassedUsersList?.Count ?? 0),

                UsersByUniversity = profiles
                    .GroupBy(p => p.User.UniversityName)
                    .ToDictionary(g => g.Key, g => g.Count()),
                UsersByCity = profiles
                    .GroupBy(p => p.City ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }

        public async Task SeedTestDataAsync(int userCount = 50)
        {
            try
            {
                _logger.LogInformation("Starting to seed {Count} test users...", userCount);

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var createdUsers = new List<(ApplicationUser User, UserProfile Profile)>();
                    var faker = new Faker();

                    // Kullanıcıları oluştur
                    for (int i = 0; i < userCount; i++)
                    {
                        var (user, profile) = await CreateUserAndProfileAsync(faker, i);
                        if (user != null && profile != null)
                        {
                            createdUsers.Add((user, profile));
                            _logger.LogInformation("✓ {Index}/{Total} - {Name} {Surname} ({Uni}) - {Type}",
                                i + 1, userCount, user.FirstName, user.LastName,
                                user.UniversityName, profile.IsPremium ? "Premium" : "Free");
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Etkileşimleri oluştur
                    _logger.LogInformation("Creating interactions...");
                    await CreateInteractionsAsync(createdUsers, faker);

                    await transaction.CommitAsync();

                    _logger.LogInformation("✅ Successfully seeded {Count} users!", createdUsers.Count);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during seeding. Transaction rolled back.");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed test data");
                throw;
            }
        }

        private async Task<(ApplicationUser?, UserProfile?)> CreateUserAndProfileAsync(Faker faker, int index)
        {
            try
            {
                var random = new Random(Guid.NewGuid().GetHashCode());
                var isMale = random.Next(2) == 0;
                var gender = isMale ? GenderType.Male : GenderType.Female;

                var firstName = isMale
                    ? MaleNames[random.Next(MaleNames.Count)]
                    : FemaleNames[random.Next(FemaleNames.Count)];

                var lastName = Surnames[random.Next(Surnames.Count)];
                var university = Universities[random.Next(Universities.Count)];
                var age = random.Next(18, 26);
                var dateOfBirth = DateTime.Now.AddYears(-age).AddDays(-random.Next(365));

                // Lokasyon
                var isIstanbul = random.Next(10) < 7;
                var districts = isIstanbul ? IstanbulDistricts : AnkaraDistricts;
                var city = isIstanbul ? "İstanbul" : "Ankara";
                var location = districts[random.Next(districts.Count)];

                // ⭐ TÜRKÇE KARAKTERLERİ TEMİZLE
                var cleanFirstName = RemoveTurkishCharacters(firstName);
                var cleanLastName = RemoveTurkishCharacters(lastName);
                var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8); // Sadece hex karakterler

                // User oluştur
                var user = new ApplicationUser
                {
                    // ⭐ Username: Sadece harf ve rakam (nokta yok, türkçe karakter yok)
                    UserName = $"{cleanFirstName.ToLower()}{cleanLastName.ToLower()}{uniqueId}",

                    // Email: Türkçe karakterler temizlenmiş
                    Email = $"{cleanFirstName.ToLower()}.{cleanLastName.ToLower()}.{uniqueId}@{university.Domain}",

                    FirstName = firstName, // Orijinal isim (Türkçe karakterli)
                    LastName = lastName,   // Orijinal soyisim (Türkçe karakterli)
                    DateOfBirth = dateOfBirth,
                    Gender = gender,
                    UniversityDomain = university.Domain,
                    UniversityName = university.Name,
                    PhoneNumber = $"+905{random.Next(300, 599)}{random.Next(100, 999)}{random.Next(1000, 9999)}",
                    EmailConfirmed = true,
                    EmailVerifiedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                    IsUniversityVerified = true,
                    LastVerificationCheck = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, "Test123!");

                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to create user {Email}: {Errors}",
                        user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return (null, null);
                }

                // Premium belirleme (%25 premium)
                var isPremium = random.Next(4) == 0;

                // Profile oluştur
                var profile = new UserProfile
                {
                    UserId = user.Id,
                    User = user,
                    DisplayName = firstName, // Display name Türkçe karakterli olabilir
                    Bio = BioTemplates[random.Next(BioTemplates.Count)],
                    Height = random.Next(155, 195),
                    Department = Departments[random.Next(Departments.Count)],
                    YearOfStudy = random.Next(1, 5),

                    // Lokasyon
                    Latitude = location.Lat + (random.NextDouble() - 0.5) * 0.05,
                    Longitude = location.Lng + (random.NextDouble() - 0.5) * 0.05,
                    City = city,
                    District = location.District,
                    LastLocationUpdate = DateTime.UtcNow.AddHours(-random.Next(1, 48)),

                    // Tercihler
                    InterestedIn = random.Next(10) < 8
                    ? (isMale ? InterestedInType.Women : InterestedInType.Men)
                    : InterestedInType.Everyone,
                    AgeRangeMin = 18,
                    AgeRangeMax = isPremium ? random.Next(25, 35) : 30,
                    MaxDistance = isPremium ? random.Next(20, 100) : 50,

                    // Gizlilik
                    ShowMyUniversity = random.Next(10) < 8,
                    ShowMeOnApp = true,
                    ShowDistance = random.Next(10) < 7,
                    ShowAge = random.Next(10) < 9,

                    // Listeler
                    MatchesList = new List<UsersDto>(),
                    LikedUsersList = new List<UsersDto>(),
                    ReceivedLikesList = new List<UsersDto>(),
                    PassedUsersList = new List<UsersDto>(),
                    BlockedUsersList = new List<UsersDto>(),
                    ReportedUsersList = new List<UsersDto>(),
                    PhotosList = new List<Photo>(),

                    // Doğrulama
                    IsPhotoVerified = random.Next(10) < 6,
                    PhotoVerifiedAt = random.Next(10) < 6
                        ? DateTime.UtcNow.AddDays(-random.Next(1, 60))
                        : null,

                    // İstatistikler
                    ProfileCompletionScore = random.Next(70, 100),
                    DailySwipeCount = random.Next(0, 30),
                    SwipeCountResetAt = DateTime.UtcNow.Date,
                    SuperLikeCount = isPremium ? random.Next(1, 6) : 1,
                    TotalMatchCount = 0,
                    TotalLikesReceived = random.Next(0, 100),

                    // Premium
                    IsPremium = isPremium,
                    PremiumExpiresAt = isPremium
                        ? DateTime.UtcNow.AddMonths(random.Next(1, 12))
                        : null,

                    // Premium Filtreler
                    PreferredUniversityDomain = isPremium && random.Next(2) == 0
                        ? Universities[random.Next(Universities.Count)].Domain
                        : null,
                    PreferredCity = isPremium && random.Next(3) == 0
                        ? city
                        : null,
                    PreferredDepartment = isPremium && random.Next(3) == 0
                        ? Departments[random.Next(Departments.Count)]
                        : null,

                    // Aktivite
                    MessagesSent = random.Next(0, 200),
                    MessagesReceived = random.Next(0, 200),
                    ResponseRate = random.NextDouble() * 100,

                    // Sosyal
                    InstagramUsername = random.Next(2) == 0
                        ? $"{cleanFirstName.ToLower()}_{cleanLastName.ToLower()}"
                        : null,

                    // Timestamps
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 90)),
                    UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 7)),
                    LastActiveAt = DateTime.UtcNow.AddHours(-random.Next(1, 48)),
                    IsActive = true,
                    IsProfileCompleted = true,

                    ProfileImageUrl = $"https://i.pravatar.cc/400?img={index + 1}"
                };

                // Fotoğraflar ekle (2-5 adet)
                int photoCount = random.Next(2, 6);
                for (int j = 0; j < photoCount; j++)
                {
                    profile.PhotosList.Add(new Photo
                    {
                        PhotoImageUrl = $"https://i.pravatar.cc/400?img={index + 1}&v={j}",
                        Order = j,
                        IsMainPhoto = j == 0,
                        IsVerified = profile.IsPhotoVerified,
                        ImageStatus = "Approved",
                        UploadedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60)),
                        Profile = profile
                    });
                }

                _context.UserProfiles.Add(profile);

                return (user, profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user and profile at index {Index}", index);
                return (null, null);
            }
        }

        /// <summary>
        /// Türkçe karakterleri İngilizce karşılıklarına çevirir
        /// </summary>
        private string RemoveTurkishCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var turkishChars = new Dictionary<char, char>
    {
        {'ç', 'c'}, {'Ç', 'C'},
        {'ğ', 'g'}, {'Ğ', 'G'},
        {'ı', 'i'}, {'İ', 'I'},
        {'ö', 'o'}, {'Ö', 'O'},
        {'ş', 's'}, {'Ş', 'S'},
        {'ü', 'u'}, {'Ü', 'U'}
    };

            var result = new System.Text.StringBuilder();
            foreach (var c in text)
            {
                if (turkishChars.ContainsKey(c))
                    result.Append(turkishChars[c]);
                else
                    result.Append(c);
            }

            return result.ToString();
        }

        private async Task CreateInteractionsAsync(
    List<(ApplicationUser User, UserProfile Profile)> users,
    Faker faker)
        {
            var random = new Random();

            foreach (var (user, profile) in users)
            {
                try
                {
                    // Rastgele like'lar (5-15 kişi)
                    var likeCount = random.Next(5, 16);
                    var potentialLikes = users
                        .Where(u => u.User.Id != user.Id && IsGenderCompatible(profile, u.Profile))
                        .OrderBy(x => random.Next())
                        .Take(likeCount)
                        .ToList();

                    foreach (var (targetUser, targetProfile) in potentialLikes)
                    {
                        // ⭐ HELPER METHOD İLE YENİ INSTANCE OLUŞTUR
                        var likeDto = CreateUsersDto(targetUser, targetProfile);
                        likeDto.IsSuperLike = random.Next(10) == 0;

                        profile.LikedUsersList.Add(likeDto);

                        // %30 match olasılığı
                        if (random.Next(10) < 3)
                        {
                            // ⭐ MATCH İÇİN YENİ INSTANCE
                            var matchDto = CreateUsersDto(targetUser, targetProfile);
                            profile.MatchesList.Add(matchDto);
                            profile.TotalMatchCount++;

                            // ⭐ Karşı taraf için de YENİ INSTANCE
                            var reverseMatchDto = CreateUsersDto(user, profile);
                            targetProfile.MatchesList.Add(reverseMatchDto);
                            targetProfile.TotalMatchCount++;
                        }
                    }

                    // Rastgele pass'ler (10-20 kişi)
                    var passCount = random.Next(10, 21);
                    var potentialPasses = users
                        .Where(u => u.User.Id != user.Id &&
                                   !profile.LikedUsersList.Any(l => l.UserId == u.User.Id))
                        .OrderBy(x => random.Next())
                        .Take(passCount)
                        .ToList();

                    foreach (var (targetUser, targetProfile) in potentialPasses)
                    {
                        // ⭐ PASS İÇİN YENİ INSTANCE
                        var passDto = CreateUsersDto(targetUser, targetProfile);
                        passDto.IsPassed = true;
                        profile.PassedUsersList.Add(passDto);
                    }

                    _logger.LogDebug("  → {DisplayName}: {LikeCount} likes, {MatchCount} matches, {PassCount} passes",
                        profile.DisplayName, profile.LikedUsersList.Count,
                        profile.MatchesList.Count, profile.PassedUsersList.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating interactions for user {UserId}", user.Id);
                }
            }

            // ⭐ SaveChanges'i tümü bitince çağır
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Her seferinde yeni bir UsersDto instance oluşturur (tracking problemini önler)
        /// </summary>
        private UsersDto CreateUsersDto(ApplicationUser user, UserProfile profile)
        {
            return new UsersDto
            {
                Id = user.Id,
                UserId = user.Id,
                Name = user.FirstName,
                Surname = user.LastName,
                DisplayName = profile.DisplayName,
                Gender = user.Gender,
                Email = user.Email,
                ProfileImageUrl = profile.ProfileImageUrl,
                Age = CalculateAge(user.DateOfBirth),
                UniversityName = user.UniversityName,
                IsVerified = profile.IsPhotoVerified
            };
        }

        private bool IsGenderCompatible(UserProfile user1, UserProfile user2)
        {
            var user1Gender = user1.User.Gender;
            var user2Gender = user2.User.Gender;
            var user1InterestedIn = user1.InterestedIn;
            var user2InterestedIn = user2.InterestedIn;

            bool user1Interested = user1InterestedIn == InterestedInType.Everyone ||
                      (user1InterestedIn == InterestedInType.Men && user2Gender == GenderType.Male) ||
                      (user1InterestedIn == InterestedInType.Women && user2Gender == GenderType.Female);

            bool user2Interested = user2InterestedIn == InterestedInType.Everyone ||
                                  (user2InterestedIn == InterestedInType.Men && user1Gender == GenderType.Male) ||
                                  (user2InterestedIn == InterestedInType.Women && user1Gender == GenderType.Female);

            return user1Interested && user2Interested;
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        public async Task ClearAllDataAsync()
        {
            try
            {
                _logger.LogWarning("Starting to clear all seed data...");

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Photos'ları sil
                    var photos = await _context.Set<Photo>().ToListAsync();
                    _context.Set<Photo>().RemoveRange(photos);

                    // Profiles'ları sil
                    var profiles = await _context.UserProfiles.ToListAsync();
                    _context.UserProfiles.RemoveRange(profiles);

                    // Users'ları sil
                    var users = await _userManager.Users.ToListAsync();
                    foreach (var user in users)
                    {
                        await _userManager.DeleteAsync(user);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogWarning("✅ All seed data cleared successfully!");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error clearing data. Transaction rolled back.");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear seed data");
                throw;
            }
        }
    }
}