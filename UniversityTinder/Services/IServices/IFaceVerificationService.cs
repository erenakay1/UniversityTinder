namespace UniversityTinder.Services.IServices
{
    /// <summary>
    /// Yüz doğrulama servisi - Fotoğraflar arasında yüz karşılaştırması yapar
    /// </summary>
    public interface IFaceVerificationService
    {
        /// <summary>
        /// İki fotoğrafı karşılaştırır ve aynı kişiye ait olup olmadığını kontrol eder
        /// </summary>
        /// <param name="sourceImage">Referans fotoğraf (ana profil fotoğrafı)</param>
        /// <param name="targetImage">Karşılaştırılacak fotoğraf</param>
        /// <returns>Karşılaştırma sonucu (similarity score ve success durumu)</returns>
        Task<FaceVerificationResult> CompareFacesAsync(IFormFile sourceImage, IFormFile targetImage);

        /// <summary>
        /// Fotoğrafta yüz var mı kontrol eder
        /// </summary>
        /// <param name="image">Kontrol edilecek fotoğraf</param>
        /// <returns>Yüz tespit sonucu</returns>
        Task<FaceDetectionResult> DetectFaceAsync(IFormFile image);

        /// <summary>
        /// Stream olarak verilen iki fotoğrafı karşılaştırır
        /// </summary>
        Task<FaceVerificationResult> CompareFacesAsync(Stream sourceImageStream, Stream targetImageStream);

        /// <summary>
        /// Stream olarak verilen fotoğrafta yüz var mı kontrol eder
        /// </summary>
        Task<FaceDetectionResult> DetectFaceAsync(Stream imageStream);
    }

    /// <summary>
    /// Yüz doğrulama sonucu
    /// </summary>
    public class FaceVerificationResult
    {
        /// <summary>
        /// Yüzler aynı kişiye mi ait
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// Benzerlik skoru (0-100)
        /// </summary>
        public float? Similarity { get; set; }

        /// <summary>
        /// Güven seviyesi (0-100)
        /// </summary>
        public float? Confidence { get; set; }

        /// <summary>
        /// İşlem başarılı mı
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Hata mesajı (varsa)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detaylı açıklama
        /// </summary>
        public string? Details { get; set; }
    }

    /// <summary>
    /// Yüz tespit sonucu
    /// </summary>
    public class FaceDetectionResult
    {
        /// <summary>
        /// Fotoğrafta yüz var mı
        /// </summary>
        public bool FaceDetected { get; set; }

        /// <summary>
        /// Tespit edilen yüz sayısı
        /// </summary>
        public int FaceCount { get; set; }

        /// <summary>
        /// Güven seviyesi (0-100)
        /// </summary>
        public float? Confidence { get; set; }

        /// <summary>
        /// İşlem başarılı mı
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Hata mesajı (varsa)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detaylı açıklama
        /// </summary>
        public string? Details { get; set; }
    }
}