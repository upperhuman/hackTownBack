using Microsoft.EntityFrameworkCore;

namespace HackTownBack.Models
{
    public class HackTownDbContext : DbContext
    {
        public HackTownDbContext(DbContextOptions<HackTownDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<UserRequest> UserRequests { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<EventRoute> EventRoutes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasMany(u => u.UserRequests)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<EventRoute>()
                .HasMany(er => er.Locations)
                .WithOne(l => l.EventRoute)
                .HasForeignKey(l => l.EventId);

            modelBuilder.Entity<EventRoute>()
                .HasMany(er => er.UserRequests)
                .WithOne(ur => ur.EventRoute)
                .HasForeignKey(ur => ur.EventRoutesId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
