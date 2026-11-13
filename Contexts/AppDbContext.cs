using Microsoft.EntityFrameworkCore;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;

namespace BE_ZSM.Contexts
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options):base(options){}
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Todo> Todos { get; set; }
        public DbSet<TodoCategory> TodoCategories { get; set; }
        public DbSet<TodoActivity> TodoActivities { get; set; }
        public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
        public DbSet<AdminRecoveryAudit> AdminRecoveryAudits => Set<AdminRecoveryAudit>();

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
                entity.HasIndex(user => user.IsSystemOwner)
                    .IsUnique()
                    .HasFilter("[IsSystemOwner] = 1");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
                entity.HasIndex(r => r.Name).IsUnique();
                entity.Property(r => r.NameNormalized)
                    .HasMaxLength(50)
                    .HasComputedColumnSql("UPPER([Name])", stored: true);
                entity.HasIndex(r => r.NameNormalized).IsUnique();
                entity.Property(r => r.Id).ValueGeneratedOnAdd();
                entity.HasData(
                    new Role { Id = 2, Name = "Admin", Description = "Administrator" },
                    new Role { Id = 1, Name = "User", Description = "Regular User" }
                );
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });

                entity.HasOne(userRole => userRole.User)
                    .WithMany(user => user.UserRoles)
                    .HasForeignKey(userRole => userRole.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(userRole => userRole.Role)
                    .WithMany(role => role.UserRoles)
                    .HasForeignKey(userRole => userRole.RoleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(userRole => userRole.RoleId);
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(permission => permission.Id);

                entity.Property(permission => permission.Code)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(permission => permission.Description)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.HasIndex(permission => permission.Code)
                    .IsUnique();
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

                entity.HasOne(rolePermission => rolePermission.Role)
                    .WithMany(role => role.RolePermissions)
                    .HasForeignKey(rolePermission => rolePermission.RoleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(rolePermission => rolePermission.Permission)
                    .WithMany(permission => permission.RolePermissions)
                    .HasForeignKey(rolePermission => rolePermission.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(rolePermission => rolePermission.PermissionId);
            });

            modelBuilder.Entity<AdminRecoveryAudit>(entity =>
            {
                entity.HasKey(audit => audit.Id);
                entity.Property(audit => audit.Action).IsRequired().HasMaxLength(100);
                entity.Property(audit => audit.Reason).IsRequired().HasMaxLength(1000);
                entity.Property(audit => audit.Outcome).IsRequired().HasMaxLength(50);
                entity.Property(audit => audit.CreatedAtUtc).IsRequired();
                entity.HasIndex(audit => audit.ActorUserId);
                entity.HasIndex(audit => audit.TargetUserId);
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

                entity.HasOne(t => t.Category)
                    .WithMany(c => c.Todos)
                    .HasForeignKey(t => t.CategoryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(t => t.Activities)
                    .WithOne(a => a.Todo)
                    .HasForeignKey(a => a.TodoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TodoCategory>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasOne(c => c.User)
                    .WithMany(u => u.TodoCategories)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TodoActivity>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Type)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(a => a.Description)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(a => a.CreatedAt)
                    .IsRequired();
            });

            modelBuilder.Entity<ExternalLogin>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.ProviderUserId)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(x => x.Email)
                    .HasMaxLength(255);

                entity.HasIndex(x => new
                {
                    x.Provider,
                    x.ProviderUserId
                }).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany(x => x.ExternalLogins)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}

