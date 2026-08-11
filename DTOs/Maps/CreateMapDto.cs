namespace BE_ZSM.DTOs.Maps
{
    public class CreateMapDto
    {
        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }
    }
}