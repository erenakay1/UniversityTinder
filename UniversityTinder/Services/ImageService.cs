using Amazon.Runtime;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon;
using UniversityTinder.Services.IServices;
using static Utility.SD;

namespace UniversityTinder.Services
{
    public class ImageService : IImageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ImageService> _logger;
        private readonly string _bucketName;

        public ImageService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ImageService> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;

            // Get AWS settings directly from configuration
            _bucketName = _configuration["AWS:BucketName"];
            string accessKey = _configuration["AWS:AccessKey"];
            string secretKey = _configuration["AWS:SecretKey"];
            string regionName = _configuration["AWS:Region"];

            // Create S3 client with direct credentials
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var region = RegionEndpoint.GetBySystemName(regionName);

            // Configure S3 client with appropriate timeout and connection settings
            var config = new AmazonS3Config
            {
                RegionEndpoint = region,
                Timeout = TimeSpan.FromSeconds(30),
                MaxErrorRetry = 3
            };

            _s3Client = new AmazonS3Client(credentials, config);

            _logger.LogInformation($"S3 client initialized for bucket: {_bucketName} in region: {regionName}");
        }

        //- S3'ten fotoğrafı byte array olarak indir (Face Verification için)
        public async Task<byte[]?> GetImageBytesAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    _logger.LogWarning("Boş URL ile dosya indirme işlemi yapılamaz");
                    return null;
                }

                _logger.LogInformation("S3'ten fotoğraf indiriliyor: {ImageUrl}", imageUrl);

                // URL'den S3 key'ini çıkar
                var s3Key = ExtractS3KeyFromUrl(imageUrl);

                if (string.IsNullOrEmpty(s3Key))
                {
                    _logger.LogWarning("URL'den S3 key çıkarılamadı: {ImageUrl}", imageUrl);
                    return null;
                }

                // S3'ten dosyayı indir
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                using (var response = await _s3Client.GetObjectAsync(request))
                using (var memoryStream = new MemoryStream())
                {
                    await response.ResponseStream.CopyToAsync(memoryStream);
                    var bytes = memoryStream.ToArray();

                    _logger.LogInformation("S3'ten fotoğraf indirildi. Key: {Key}, Size: {Size} bytes",
                        s3Key, bytes.Length);

                    return bytes;
                }
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("S3'te dosya bulunamadı: {ImageUrl}", imageUrl);
                    return null;
                }

                _logger.LogError(ex, "S3'ten fotoğraf indirme hatası: {ImageUrl}, ErrorCode={ErrorCode}",
                    imageUrl, ex.ErrorCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fotoğraf indirme hatası: {ImageUrl}", imageUrl);
                return null;
            }
        }

        public ImageSaveResult SaveImage(IFormFile image, string folderName, string objectId)
        {
            try
            {
                _logger.LogInformation($"Dosya yükleme başladı. BucketName: {_bucketName}, Klasör: {folderName}, ProfilID: {objectId}");

                // Validate image
                var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                string fileExtension = Path.GetExtension(image.FileName);
                if (!validExtensions.Contains(fileExtension.ToLower()))
                {
                    throw new ArgumentException("Invalid file type.");
                }

                // Generate key for S3 (equivalent to filename)
                string s3Key = $"{folderName}/{objectId}{fileExtension}";

                // Read file into memory to avoid stream issues
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    image.CopyTo(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                _logger.LogInformation($"Dosya belleğe kopyalandı. Boyut: {fileBytes.Length} bytes");

                // Upload file to S3 synchronously to match interface
                using (var memoryStream = new MemoryStream(fileBytes))
                {
                    var uploadRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = s3Key,
                        InputStream = memoryStream,
                        ContentType = image.ContentType,
                    };

                    // Use synchronous method but force its completion properly
                    var response = _s3Client.PutObjectAsync(uploadRequest)
                                           .ConfigureAwait(false)
                                           .GetAwaiter()
                                           .GetResult();

                    _logger.LogInformation($"S3 yükleme tamamlandı. HTTP Durum: {response.HttpStatusCode}");
                }

                // Generate S3 URL
                string imageUrl = $"https://{_bucketName}.s3.amazonaws.com/{s3Key}";

                _logger.LogInformation($"Dosya başarıyla yüklendi. URL: {imageUrl}");

                return new ImageSaveResult
                {
                    ImageUrl = imageUrl,
                    LocalPath = s3Key
                };
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, $"S3 hatası: {ex.Message}. Error Code: {ex.ErrorCode}, Request ID: {ex.RequestId}");
                throw new Exception($"S3 yükleme hatası: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Beklenmeyen hata: {ex.Message}");
                throw new Exception($"Dosya yükleme hatası: {ex.Message}", ex);
            }
        }

        //- Tek dosya silme
        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    _logger.LogWarning("Boş URL ile silme işlemi yapılamaz");
                    return true; // Zaten yok, başarılı sayılabilir
                }

                // URL'den S3 key'ini çıkar
                // Örnek: https://bucketname.s3.amazonaws.com/ProfileImages/123.jpg -> ProfileImages/123.jpg
                var s3Key = ExtractS3KeyFromUrl(imageUrl);

                if (string.IsNullOrEmpty(s3Key))
                {
                    _logger.LogWarning("URL'den S3 key çıkarılamadı: {ImageUrl}", imageUrl);
                    return false;
                }

                _logger.LogInformation("S3'ten dosya siliniyor: Bucket={BucketName}, Key={Key}", _bucketName, s3Key);

                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                var response = await _s3Client.DeleteObjectAsync(deleteRequest);

                _logger.LogInformation("S3'ten dosya başarıyla silindi: {Key}, StatusCode={StatusCode}",
                    s3Key, response.HttpStatusCode);

                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent ||
                       response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (AmazonS3Exception ex)
            {
                // 404 NotFound normal karşılanabilir (dosya zaten yok)
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Silinmek istenen dosya bulunamadı (zaten yok): {ImageUrl}", imageUrl);
                    return true;
                }

                _logger.LogError(ex, "S3'ten dosya silinirken hata: {ImageUrl}, ErrorCode={ErrorCode}",
                    imageUrl, ex.ErrorCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silinirken beklenmeyen hata: {ImageUrl}", imageUrl);
                return false;
            }
        }

        //- Toplu dosya silme (prefix ile)
        public async Task<bool> DeleteImagesByPrefixAsync(string prefix)
        {
            try
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    _logger.LogWarning("Boş prefix ile silme işlemi yapılamaz");
                    return false;
                }

                _logger.LogInformation("S3'ten prefix ile dosyalar listeleniyor: Bucket={BucketName}, Prefix={Prefix}",
                    _bucketName, prefix);

                // Prefix'e göre tüm dosyaları listele
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                };

                var listResponse = await _s3Client.ListObjectsV2Async(listRequest);

                if (listResponse.S3Objects.Count == 0)
                {
                    _logger.LogInformation("Silinecek dosya bulunamadı: {Prefix}", prefix);
                    return true;
                }

                _logger.LogInformation("Toplam {Count} dosya bulundu, silme işlemi başlatılıyor",
                    listResponse.S3Objects.Count);

                // Tüm dosyaları sil (max 1000 dosya)
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = listResponse.S3Objects.Select(obj => new KeyVersion { Key = obj.Key }).ToList()
                };

                var deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest);

                _logger.LogInformation("S3'ten {Count} dosya başarıyla silindi: {Prefix}",
                    deleteResponse.DeletedObjects.Count, prefix);

                // Eğer hata varsa logla
                if (deleteResponse.DeleteErrors.Count > 0)
                {
                    foreach (var error in deleteResponse.DeleteErrors)
                    {
                        _logger.LogError("Dosya silinemedi: Key={Key}, ErrorCode={ErrorCode}, Message={Message}",
                            error.Key, error.Code, error.Message);
                    }
                    return false;
                }

                return true;
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "S3'ten toplu dosya silinirken hata: {Prefix}, ErrorCode={ErrorCode}",
                    prefix, ex.ErrorCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu dosya silinirken beklenmeyen hata: {Prefix}", prefix);
                return false;
            }
        }

        // ✅ Helper metod - URL'den S3 key çıkar
        private string ExtractS3KeyFromUrl(string imageUrl)
        {
            try
            {
                // Format 1: https://bucketname.s3.amazonaws.com/folder/file.jpg
                // Format 2: https://bucketname.s3.region.amazonaws.com/folder/file.jpg
                // Format 3: https://s3.region.amazonaws.com/bucketname/folder/file.jpg

                var uri = new Uri(imageUrl);
                var path = uri.AbsolutePath.TrimStart('/');

                // Eğer URL bucket adını içeriyorsa çıkar
                if (path.StartsWith(_bucketName + "/"))
                {
                    path = path.Substring(_bucketName.Length + 1);
                }

                return path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "URL parse hatası: {ImageUrl}", imageUrl);
                return null;
            }
        }
    }
}
