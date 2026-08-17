using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Vehicles;

public class VehicleResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public VehicleType Type { get; set; }
    public VehicleRank? Rank { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}