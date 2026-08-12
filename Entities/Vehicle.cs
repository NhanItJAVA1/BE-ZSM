using BE_ZSM.Enums;

namespace BE_ZSM.Entities
{
    public class Vehicle
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public VehicleType Type { get; set; }

        public VehicleRank? Rank { get; set; }

        public DateTime CreatedAt { get; set; }


        public string? ImageUrl { get; set; }

        public ICollection<Record> Records { get; set; } = new List<Record>();
    }
}
