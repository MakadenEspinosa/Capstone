using ESEvents.Domain.Departments;
using ESEvents.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentMemberConfiguration : IEntityTypeConfiguration<DepartmentMember>
{
    public void Configure(EntityTypeBuilder<DepartmentMember> b)
    {
        b.ToTable("department_members");
        b.HasKey(m => new { m.DepartmentId, m.UserId });
        b.Property(m => m.AddedAtUtc).IsRequired();
        b.HasOne<ApplicationIdentityUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(m => m.UserId);
    }
}
