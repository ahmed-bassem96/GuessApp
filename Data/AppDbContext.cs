using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.HasIndex(u => u.Email).IsUnique();
        user.Property(u => u.Email).HasMaxLength(254).IsRequired();
        user.Property(u => u.PasswordHash).IsRequired();
        user.Property(u => u.Version).IsRowVersion();
    }
}
