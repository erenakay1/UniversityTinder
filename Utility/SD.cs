namespace Utility
{
    public class SD
    {

        public static string UniversityTinderBase { get; set; }
        public enum ApiType
        {
            GET,
            POST,
            PUT,
            DELETE
        }

        public const string TokenCookie = "JWTToken";
        public static string AccessToken = "JWTToken";
        public static string RefreshToken = "RefreshToken";
        public static string CurrentAPIVersion = "v2";
        public const string Admin = "ADMIN";
        public const string Customer = "CUSTOMER";
        public const string User = "USER";
        public const string Premium = "PREMIUM";
        public const string Man = "Man";
        public const string Woman = "Woman";

        public enum ContentType
        {
            Json,
            MultipartFormData,
        }


        public class ImageSaveResult
        {
            public string ImageUrl { get; set; }
            public string LocalPath { get; set; }
        }
    }
}
