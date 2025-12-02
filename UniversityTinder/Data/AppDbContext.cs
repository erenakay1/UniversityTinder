using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UniversityTinder.Models;

namespace UniversityTinder.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            //modelBuilder.Entity<ApplicationUser>()
            //    .HasOne<Profile>()
            //    .WithOne(p => p.User)
            //    .HasForeignKey<Profile>(p => p.UserId)
            //    .OnDelete(DeleteBehavior.NoAction);


            // ApplicationUser
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Gender).IsRequired().HasMaxLength(20);
                entity.Property(e => e.UniversityDomain).IsRequired().HasMaxLength(100);

                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.UniversityDomain);
            });

            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(e => e.ProfileId);

                entity.Property(e => e.PhotosList)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<List<Photo>>(v, (JsonSerializerOptions)null))
                    .HasColumnType("nvarchar(max)");
            });


        }
    }
}