namespace BE_ZSM.DTOs.Vehicles
{
    public class CreateVehicleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}