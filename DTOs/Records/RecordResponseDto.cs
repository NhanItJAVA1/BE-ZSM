using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Records
{
    public class RecordResponseDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? VideoUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public double FinishTime { get; set; }
        public string? Description { get; set; }
        public int Views { get; set; }
        public RecordStatus Status { get; set; }
        public string? RejectReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public UserMinimalDto? User { get; set; }
        public MapMinimalDto? Map { get; set; }
        public VehicleMinimalDto? Vehicle { get; set; }
        public GameModeMinimalDto? GameMode { get; set; }
    }


    public class UserMinimalDto
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }

    public class MapMinimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Rate { get; set; }
        public string? ImageUrl { get; set; }

    }

    public class VehicleMinimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public VehicleRank? Rank { get; internal set; }
        public VehicleType Type { get; internal set; }
    }

    public class GameModeMinimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
