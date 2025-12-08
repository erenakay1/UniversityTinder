namespace UniversityTinder.Models.Dto
{
    public class SwipeStatsDto
    {
        public int TotalSwipesToday { get; set; }
        public int RemainingSwipes { get; set; }  // -1 = unlimited (premium)
        public int SuperLikesRemaining { get; set; }
        public DateTime SwipeCountResetAt { get; set; }
        public bool IsPremium { get; set; }

        // Bugünkü aktivite
        public int LikesToday { get; set; }
        public int PassesToday { get; set; }
        public int SuperLikesToday { get; set; }
        public int MatchesToday { get; set; }
    }
}
