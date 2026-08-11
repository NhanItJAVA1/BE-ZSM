namespace BE_ZSM.Entities
{
    public class Map
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<Record> Records { get; set; } = new List<Record>();
    }
}
