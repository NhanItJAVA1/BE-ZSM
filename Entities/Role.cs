using BE_ZSM.Enums;

namespace BE_ZSM.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public UserRole Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public ICollection<User> Users { get; set; } = new List<User>();

    }
}
