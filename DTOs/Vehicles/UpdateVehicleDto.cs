namespace BE_ZSM.DTOs.Vehicles
{
    public class UpdateVehicleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}