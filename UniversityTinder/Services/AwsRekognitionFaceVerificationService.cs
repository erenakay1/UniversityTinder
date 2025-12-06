using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Org.BouncyCastle.Security;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Services
{
    /// <summary>
    /// AWS Rekognition kullanarak yüz doğrulama servisi
    /// </summary>
    public class AwsRekognitionFaceVerificationService : IFaceVerificationService
    {
        private readonly IAmazonRekognition _rekognitionClient;
        private readonly ILogger<AwsRekognitionFaceVerificationService> _logger;
        private readonly float _similarityThreshold = 80f; // %80 benzerlik eşiği

        public AwsRekognitionFaceVerificationService(
            IAmazonRekognition rekognitionClient,
            ILogger<AwsRekognitionFaceVerificationService> logger)
        {
            _rekognitionClient = rekognitionClient;
            _logger = logger;
        }

        /// <summary>
        /// İki fotoğrafı karşılaştırır (IFormFile)
        /// </summary>
        public async Task<FaceVerificationResult> CompareFacesAsync(IFormFile sourceImage, IFormFile targetImage)
        {
            try
            {
                using var sourceStream = sourceImage.OpenReadStream();
                using var targetStream = targetImage.OpenReadStream();

                return await CompareFacesAsync(sourceStream, targetStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompareFacesAsync hatası (IFormFile)");
                return new FaceVerificationResult
                {
                    IsSuccess = false,
                    IsMatch = false,
                    ErrorMessage = $"Yüz karşılaştırma hatası: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// İki fotoğrafı karşılaştırır (Stream)
        /// </summary>
        public async Task<FaceVerificationResult> CompareFacesAsync(Stream sourceImageStream, Stream targetImageStream)
        {
            try
            {
                _logger.LogInformation("AWS Rekognition yüz karşılaştırma başlatıldı");

                // Source image'ı byte array'e çevir
                using var sourceMs = new MemoryStream();
                await sourceImageStream.CopyToAsync(sourceMs);
                var sourceBytes = sourceMs.ToArray();

                // Target image'ı byte array'e çevir
                using var targetMs = new MemoryStream();
                await targetImageStream.CopyToAsync(targetMs);
                var targetBytes = targetMs.ToArray();

                // AWS Rekognition request oluştur
                var request = new CompareFacesRequest
                {
                    SourceImage = new Image
                    {
                        Bytes = new MemoryStream(sourceBytes)
                    },
                    TargetImage = new Image
                    {
                        Bytes = new MemoryStream(targetBytes)
                    },
                    SimilarityThreshold = _similarityThreshold
                };

                // AWS Rekognition'a gönder
                var response = await _rekognitionClient.CompareFacesAsync(request);

                // Sonuçları değerlendir
                if (response.FaceMatches == null || response.FaceMatches.Count == 0)
                {
                    _logger.LogWarning("Yüzler eşleşmedi veya yüz tespit edilemedi");

                    // Kaynak veya hedef fotoğrafta yüz yoksa detaylı kontrol
                    if (response.UnmatchedFaces != null && response.UnmatchedFaces.Count > 0)
                    {
                        return new FaceVerificationResult
                        {
                            IsSuccess = true,
                            IsMatch = false,
                            Similarity = 0,
                            Confidence = 0,
                            Details = "Fotoğraflar farklı kişilere ait veya yüz tespit edilemedi"
                        };
                    }

                    return new FaceVerificationResult
                    {
                        IsSuccess = true,
                        IsMatch = false,
                        Similarity = 0,
                        Confidence = 0,
                        Details = "Yüz tespit edilemedi"
                    };
                }

                // En yüksek benzerliğe sahip eşleşmeyi al
                var bestMatch = response.FaceMatches
                    .OrderByDescending(m => m.Similarity)
                    .First();

                var similarity = bestMatch.Similarity;
                var confidence = bestMatch.Face.Confidence;
                var isMatch = similarity >= _similarityThreshold;

                _logger.LogInformation(
                    "Yüz karşılaştırma tamamlandı. Similarity: {Similarity}%, Confidence: {Confidence}%, IsMatch: {IsMatch}",
                    similarity, confidence, isMatch);

                return new FaceVerificationResult
                {
                    IsSuccess = true,
                    IsMatch = isMatch,
                    Similarity = similarity,
                    Confidence = confidence,
                    Details = $"Benzerlik: %{similarity:F2}, Güven: %{confidence:F2}"
                };
            }
            catch (Amazon.Rekognition.Model.InvalidParameterException ex)
            {
                _logger.LogWarning(ex, "Geçersiz parametre - muhtemelen fotoğrafta yüz yok");
                return new FaceVerificationResult
                {
                    IsSuccess = false,
                    IsMatch = false,
                    ErrorMessage = "Fotoğrafta yüz tespit edilemedi",
                    Details = ex.Message
                };
            }
            catch (InvalidImageFormatException ex)
            {
                _logger.LogWarning(ex, "Geçersiz fotoğraf formatı");
                return new FaceVerificationResult
                {
                    IsSuccess = false,
                    IsMatch = false,
                    ErrorMessage = "Geçersiz fotoğraf formatı",
                    Details = ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AWS Rekognition yüz karşılaştırma hatası");
                return new FaceVerificationResult
                {
                    IsSuccess = false,
                    IsMatch = false,
                    ErrorMessage = $"Yüz karşılaştırma hatası: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Fotoğrafta yüz var mı kontrol eder (IFormFile)
        /// </summary>
        public async Task<FaceDetectionResult> DetectFaceAsync(IFormFile image)
        {
            try
            {
                using var stream = image.OpenReadStream();
                return await DetectFaceAsync(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DetectFaceAsync hatası (IFormFile)");
                return new FaceDetectionResult
                {
                    IsSuccess = false,
                    FaceDetected = false,
                    ErrorMessage = $"Yüz tespit hatası: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Fotoğrafta yüz var mı kontrol eder (Stream)
        /// </summary>
        public async Task<FaceDetectionResult> DetectFaceAsync(Stream imageStream)
        {
            try
            {
                _logger.LogInformation("AWS Rekognition yüz tespiti başlatıldı");

                // Image'ı byte array'e çevir
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                var imageBytes = ms.ToArray();

                // AWS Rekognition request oluştur
                var request = new DetectFacesRequest
                {
                    Image = new Image
                    {
                        Bytes = new MemoryStream(imageBytes)
                    },
                    Attributes = new List<string> { "ALL" } // Tüm yüz özelliklerini al
                };

                // AWS Rekognition'a gönder
                var response = await _rekognitionClient.DetectFacesAsync(request);

                // Sonuçları değerlendir
                var faceCount = response.FaceDetails?.Count ?? 0;
                var faceDetected = faceCount > 0;

                if (faceDetected)
                {
                    var primaryFace = response.FaceDetails.First();
                    var confidence = primaryFace.Confidence;

                    _logger.LogInformation(
                        "Yüz tespiti tamamlandı. Face Count: {FaceCount}, Confidence: {Confidence}%",
                        faceCount, confidence);

                    return new FaceDetectionResult
                    {
                        IsSuccess = true,
                        FaceDetected = true,
                        FaceCount = faceCount,
                        Confidence = confidence,
                        Details = faceCount > 1
                            ? $"{faceCount} yüz tespit edildi (sadece 1 olmalı)"
                            : "1 yüz tespit edildi"
                    };
                }
                else
                {
                    _logger.LogWarning("Fotoğrafta yüz tespit edilemedi");
                    return new FaceDetectionResult
                    {
                        IsSuccess = true,
                        FaceDetected = false,
                        FaceCount = 0,
                        Confidence = 0,
                        Details = "Fotoğrafta yüz tespit edilemedi"
                    };
                }
            }
            catch (Amazon.Rekognition.Model.InvalidParameterException ex)
            {
                _logger.LogWarning(ex, "Geçersiz parametre - muhtemelen fotoğrafta yüz yok");
                return new FaceDetectionResult
                {
                    IsSuccess = false,
                    FaceDetected = false,
                    ErrorMessage = "Fotoğrafta yüz tespit edilemedi",
                    Details = ex.Message
                };
            }
            catch (InvalidImageFormatException ex)
            {
                _logger.LogWarning(ex, "Geçersiz fotoğraf formatı");
                return new FaceDetectionResult
                {
                    IsSuccess = false,
                    FaceDetected = false,
                    ErrorMessage = "Geçersiz fotoğraf formatı",
                    Details = ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AWS Rekognition yüz tespit hatası");
                return new FaceDetectionResult
                {
                    IsSuccess = false,
                    FaceDetected = false,
                    ErrorMessage = $"Yüz tespit hatası: {ex.Message}"
                };
            }
        }
    }
}