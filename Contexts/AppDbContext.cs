using Microsoft.EntityFrameworkCore;
using BE_ZSM.Entities;

namespace BE_ZSM.Contexts
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
       : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Map> Maps { get; set; }

        public DbSet<Vehicle> Vehicles { get; set; }

        public DbSet<GameMode> GameModes { get; set; }

        public DbSet<Record> Records { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Map>()
                .HasIndex(m => m.Slug)
                .IsUnique();

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.Slug)
                .IsUnique();
        }
    }
}
