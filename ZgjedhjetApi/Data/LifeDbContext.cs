using Microsoft.EntityFrameworkCore;
using ZgjedhjetApi.Models.Entities;

namespace ZgjedhjetApi.Data
{
    // YOUR CODE HERE
    public class LifeDbContext : DbContext
    {
        public LifeDbContext(DbContextOptions<LifeDbContext> options) : base(options)
        {
        }
        public DbSet<Zgjedhjet> Zgjedhjet { get; set; }
    }
}