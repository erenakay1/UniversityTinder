using Google.Cloud.Vision.V1;
using UniversityTinder.Models.Dto;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Services
{
    public class ImageSensorService : IImageSensorService
    {
        private readonly string _googleCredentialPath;

        public ImageSensorService()
        {
            // API JSON Anahtarının Yolunu Belirle
            _googleCredentialPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/keys/directed-symbol-417118-5ea8d66c679f.json");

            // Google API Kimlik Doğrulamasını Ayarla
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _googleCredentialPath);
        }

        public async Task<ResponseDto?> AnalyzeImageAsync(IFormFile imageFile)
        {
            var responseDto = new ResponseDto();

            if (imageFile == null || imageFile.Length == 0)
            {
                responseDto.IsSuccess = false;
                responseDto.Message = "Dosya boş ya da geçersiz!";
                responseDto.StatusCode = System.Net.HttpStatusCode.BadRequest;
                return responseDto;
            }

            try
            {
                // Geçici bir dosya yolu oluştur
                var tempFilePath = Path.GetTempFileName();

                // Dosyayı geçici alana kopyala
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                var client = await ImageAnnotatorClient.CreateAsync();
                var image = Image.FromFile(tempFilePath);

                var response = await client.DetectSafeSearchAsync(image);

                // Güvenlik analizi sonuçlarını ResponseDto.Result olarak ekle
                responseDto.Result = new
                {
                    Adult = response.Adult.ToString(),
                    Violence = response.Violence.ToString(),
                    Racy = response.Racy.ToString(),
                    Medical = response.Medical.ToString(),
                    Spoof = response.Spoof.ToString()
                };

                if (response.Adult == Likelihood.VeryLikely || response.Violence == Likelihood.VeryLikely)
                {
                    responseDto.IsSuccess = false;
                    responseDto.Message = "Resim uygunsuz içerik içeriyor!";
                    responseDto.StatusCode = System.Net.HttpStatusCode.BadRequest;
                }
                else
                {
                    responseDto.IsSuccess = true;
                    responseDto.Message = "Resim uygun içerik içeriyor!";
                    responseDto.StatusCode = System.Net.HttpStatusCode.OK;
                }

                // Geçici dosyayı sil
                File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                responseDto.IsSuccess = false;
                responseDto.Message = $"Hata: {ex.Message}";
                responseDto.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            }

            return responseDto;
        }
    }
}
