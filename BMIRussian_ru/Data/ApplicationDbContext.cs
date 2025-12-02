using Microsoft.EntityFrameworkCore;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserCredentianl> UserCredentianls { get; set; }
        public virtual DbSet<UserRoles> UserRoles { get; set; }
        public virtual DbSet<UserToken> UserToken { get; set; }

        public virtual DbSet<Channel> Channels { get; set; }
    }
}