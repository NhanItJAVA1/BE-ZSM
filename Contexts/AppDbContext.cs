using Microsoft.EntityFrameworkCore;
using BE_ZSM.Entities;

namespace BE_ZSM.Contexts
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options):base(options){}
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Map> Maps { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<GameMode> GameModes { get; set; }
        public DbSet<Record> Records { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder){
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.Property(r => r.Name).HasConversion<int>();
                entity.Property(r => r.Id).ValueGeneratedNever();
                entity.HasData(
                    new Role { Id = 2, Name = Enums.UserRole.Admin, Description = "Administrator" },
                    new Role { Id = 1, Name = Enums.UserRole.User, Description = "Regular User" }
                );
            });
            modelBuilder.Entity<Todo>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(t => t.Description)
                    .HasMaxLength(1000);

                entity.Property(t => t.Status)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(t => t.Priority)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(t => t.CreatedAt)
                    .IsRequired();

                entity.HasOne(t => t.User)
                    .WithMany(u => u.Todos)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}
