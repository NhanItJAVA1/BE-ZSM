using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Vehicles
{
    public class CreateVehicleDto
    {
        public string Name { get; set; } = string.Empty;
        public VehicleType Type { get; set; }
        public VehicleRank? Rank { get; set; }
        public string? ImageUrl { get; set; }
    }
}