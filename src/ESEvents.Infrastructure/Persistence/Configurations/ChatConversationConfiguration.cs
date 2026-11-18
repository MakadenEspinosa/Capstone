using ESEvents.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class ChatConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("chat_conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.CompanyId).IsRequired();
        builder.Property(conversation => conversation.UserId1).IsRequired();
        builder.Property(conversation => conversation.UserId2).IsRequired();
        builder.Property(conversation => conversation.UnreadCountUser1).IsRequired();
        builder.Property(conversation => conversation.UnreadCountUser2).IsRequired();
        builder.Property(conversation => conversation.IsArchivedByUser1).IsRequired();
        builder.Property(conversation => conversation.IsArchivedByUser2).IsRequired();
        builder.HasIndex(conversation => new
        {
            conversation.CompanyId,
            conversation.UserId1,
            conversation.UserId2
        }).IsUnique();
        builder.HasOne<ESEvents.Domain.Companies.Company>()
            .WithMany()
            .HasForeignKey(conversation => conversation.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(conversation => new { conversation.CompanyId, conversation.UserId1 });
        builder.HasIndex(conversation => new { conversation.CompanyId, conversation.UserId2 });
    }
}
