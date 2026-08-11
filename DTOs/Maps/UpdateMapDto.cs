namespace BE_ZSM.DTOs.Maps
{
    public class UpdateMapDto
    {
        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }
    }
}