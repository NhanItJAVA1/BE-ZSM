namespace BE_ZSM.DTOs.Maps
{
    public class MapResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Rate { get; set; }

        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; }
    }

}

