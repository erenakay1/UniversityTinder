namespace UniversityTinder.Models.Dto
{
    public class SeedStatsDto
    {
        public int TotalUsers { get; set; }
        public int MaleUsers { get; set; }
        public int FemaleUsers { get; set; }
        public int PremiumUsers { get; set; }
        public int FreeUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int TotalProfiles { get; set; }
        public int TotalPhotos { get; set; }
        public int TotalLikes { get; set; }
        public int TotalMatches { get; set; }
        public int TotalPasses { get; set; }
        public Dictionary<string, int> UsersByUniversity { get; set; } = new();
        public Dictionary<string, int> UsersByCity { get; set; } = new();
    }
}