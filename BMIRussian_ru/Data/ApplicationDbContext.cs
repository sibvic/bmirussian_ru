using Microsoft.EntityFrameworkCore;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : UserDBContext(options)
    {
        //public virtual DbSet<Channel> Channels { get; set; }
    }
}