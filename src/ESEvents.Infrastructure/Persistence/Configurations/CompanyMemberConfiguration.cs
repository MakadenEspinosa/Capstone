using ESEvents.Domain.Companies;
using ESEvents.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("company_members");
        builder.HasKey(member => new { member.CompanyId, member.UserId });
        builder.Property(member => member.AddedAtUtc).IsRequired();
        builder.HasOne<ApplicationIdentityUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(member => member.UserId);
    }
}
