namespace BE_ZSM.Entities
{
    public class GameMode
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Record> Records { get; set; } = new List<Record>();
    }
}
