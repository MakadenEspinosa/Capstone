using ESEvents.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("departments");
        b.HasKey(d => d.Id);
        b.Property(d => d.Name).HasMaxLength(Department.MaxNameLength).IsRequired();
        b.Property(d => d.NormalizedName).HasMaxLength(Department.MaxNameLength).IsRequired();
        b.Property(d => d.Description).HasMaxLength(Department.MaxDescriptionLength);
        b.HasIndex(d => d.NormalizedName).IsUnique();

        b.HasMany(d => d.Members)
            .WithOne()
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(d => d.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
