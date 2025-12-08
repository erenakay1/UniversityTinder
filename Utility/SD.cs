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

        public enum GenderType
        {
            Male,
            Female,
            Other
        }

        public enum InterestedInType
        {
            Men,
            Women,
            Everyone
        }
        public class ImageSaveResult
        {
            public string ImageUrl { get; set; }
            public string LocalPath { get; set; }
        }


        public enum Hobbies
        {
            // Fitness & Sports
            Gym,
            Yoga,
            Running,
            Swimming,
            Cycling,
            Hiking,
            RockClimbing,
            Boxing,
            MartialArts,
            Dancing,
            Pilates,

            // Food & Drink
            Cooking,
            Baking,
            WineTasting,
            CoffeeLover,
            Foodie,
            VeganCooking,
            Mixology,

            // Arts & Creativity
            Photography,
            Painting,
            Drawing,
            Writing,
            Poetry,
            Crafts,
            DIY,
            Fashion,

            // Music & Entertainment
            Music,
            Concerts,
            PlayingGuitar,
            PlayingPiano,
            Singing,
            DJing,
            Festivals,

            // Outdoor & Adventure
            Traveling,
            Camping,
            Fishing,
            Surfing,
            Skiing,
            Snowboarding,
            Gardening,
            BeachLife,

            // Culture & Learning
            Reading,
            Museums,
            ArtGalleries,
            Theater,
            Movies,
            Documentary,
            Learning,
            Languages,

            // Games & Tech
            VideoGames,
            BoardGames,
            Chess,
            Coding,
            Gaming,
            VR,
            Podcasts,

            // Social & Lifestyle
            Volunteering,
            Pets,
            Dogs,
            Cats,
            Meditation,
            Astrology,
            Shopping,
            Nightlife,
            Brunch,
            SocialDrinking,
            Networking,

            // Intellectual
            Politics,
            Philosophy,
            Science,
            History,
            Investing,
            Entrepreneurship
        }


    }
}
