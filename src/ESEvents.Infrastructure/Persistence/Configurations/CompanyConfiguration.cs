using ESEvents.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Name).HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.Property(company => company.NormalizedName).HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.HasIndex(company => company.NormalizedName).IsUnique();
        builder.HasMany(company => company.Members)
            .WithOne()
            .HasForeignKey(member => member.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(company => company.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
