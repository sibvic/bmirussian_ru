using Microsoft.EntityFrameworkCore;
using Sibvic.UserWithBalanceLib.Data;

namespace BMIRussian_ru.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : UserWithBalanceContext(options)
    {
        public virtual DbSet<Video> Videos { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }
        public virtual DbSet<StlJob> StlJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<StlJob>()
                .HasIndex(j => j.RequestId)
                .IsUnique();
            modelBuilder.Entity<StlJob>()
                .HasIndex(j => j.UserId);
        }
    }
}