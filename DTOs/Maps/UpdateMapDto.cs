namespace BE_ZSM.DTOs.Maps
{
    public class UpdateMapDto
    {
        public string Name { get; set; } = string.Empty;

        public int Rate { get; set; }

        public string? ImageUrl { get; set; }
    }
}