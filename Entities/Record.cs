namespace BE_ZSM.Entities
{
    public class Record
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int MapId { get; set; }

        public int VehicleId { get; set; }

        public int GameModeId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string VideoUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public TimeSpan FinishTime { get; set; }

        public string? Description { get; set; }

        public int Views { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }


        // Navigation Properties

        public User User { get; set; } = null!;

        public Map Map { get; set; } = null!;

        public Vehicle Vehicle { get; set; } = null!;

        public GameMode GameMode { get; set; } = null!;
    }
}
