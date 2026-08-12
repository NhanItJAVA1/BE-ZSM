using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Records
{
    /// <summary>
    /// DTO for record response with nested related data
    /// </summary>
    public class RecordResponseDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? VideoUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public double FinishTime { get; set; }
        public string? Description { get; set; }
        public int Views { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Nested DTOs instead of entities
        public UserMinimalDto? User { get; set; }
        public MapMinimalDto? Map { get; set; }
        public VehicleMinimalDto? Vehicle { get; set; }
        public GameModeMinimalDto? GameMode { get; set; }
    }

    /// <summary>
    /// Minimal user info for nested responses
    /// </summary>
    public class UserMinimalDto
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>
    /// Minimal map info for nested responses
    /// </summary>
    public class MapMinimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Rate { get; set; }
        public string? ImageUrl { get; set; }

    }

    /// <summary>
    /// Minimal vehicle info for nested responses
    /// </summary>
    public class VehicleMinimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public VehicleRank? Rank { get; internal set; }
        public VehicleType Type { get; internal set; }
    }

    /// <summary>
    /// Minimal game mode info for nested responses
    /// </summary>
    public class GameModeMinimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
