using ESEvents.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);

        b.Property(u => u.Email)
            .HasConversion(e => e.Value, v => Email.Create(v))
            .HasMaxLength(320)
            .IsRequired();
        b.HasIndex(u => u.Email).IsUnique();

        b.Property(u => u.PasswordHash).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);
        b.Property(u => u.Status).HasConversion<string>().HasMaxLength(32);

        b.ComplexProperty(u => u.Profile, p =>
        {
            p.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            p.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            p.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
            p.Property(x => x.Bio).HasColumnName("bio").HasMaxLength(1000);
        });
    }
}
