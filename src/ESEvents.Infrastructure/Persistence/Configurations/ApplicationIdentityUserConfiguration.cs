using ESEvents.Domain.Users;
using ESEvents.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationIdentityUserConfiguration : IEntityTypeConfiguration<ApplicationIdentityUser>
{
    public void Configure(EntityTypeBuilder<ApplicationIdentityUser> b)
    {
        b.ToTable("users");
        b.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        b.Property(u => u.Bio).HasColumnName("bio").HasMaxLength(1000);
        b.Property(u => u.Email).HasMaxLength(320).IsRequired();
        b.Property(u => u.NormalizedEmail).HasMaxLength(320);
        b.Property(u => u.UserName).HasMaxLength(320);
        b.Property(u => u.NormalizedUserName).HasMaxLength(320);
        b.Property(u => u.PasswordHash).IsRequired();
        b.Property(u => u.SystemRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(u => u.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(u => u.CreatedAtUtc).IsRequired();
        b.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
    }
}