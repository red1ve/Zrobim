using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zrobim.Api.Users;

namespace Zrobim.Api.Chat;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> b)
    {
        b.ToTable("conversations");

        b.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(c => c.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<User>().WithMany().HasForeignKey(c => c.ClientId);
        b.HasOne(c => c.Master).WithMany().HasForeignKey(c => c.MasterId);

        b.HasIndex(c => new { c.ClientId, c.MasterId }).IsUnique().HasDatabaseName("conversations_client_master_key");
        b.HasIndex(c => new { c.MasterId, c.LastMessageAt }).IsDescending(false, true)
            .HasDatabaseName("conversations_master_last_idx");
        b.HasIndex(c => new { c.ClientId, c.LastMessageAt }).IsDescending(false, true)
            .HasDatabaseName("conversations_client_last_idx");
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("messages", t =>
        {
            t.HasCheckConstraint("messages_not_empty",
                "(text IS NOT NULL AND btrim(text) <> '') OR attachment_key IS NOT NULL");
            t.HasCheckConstraint("messages_attachment_fields_together",
                "(attachment_key IS NULL) = (attachment_content_type IS NULL) AND (attachment_key IS NULL) = (attachment_size_bytes IS NULL)");
            t.HasCheckConstraint("messages_attachment_is_image",
                "attachment_content_type IS NULL OR attachment_content_type IN ('image/jpeg', 'image/png', 'image/webp')");
            t.HasCheckConstraint("messages_attachment_size_limit",
                "attachment_size_bytes IS NULL OR attachment_size_bytes BETWEEN 1 AND 10485760");
            t.HasCheckConstraint("messages_text_length", "text IS NULL OR length(text) <= 4000");
        });

        b.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(m => m.AttachmentKey).HasMaxLength(500);
        b.Property(m => m.AttachmentContentType).HasMaxLength(100);
        b.Property(m => m.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(m => m.Conversation).WithMany().HasForeignKey(m => m.ConversationId);
        b.HasOne<User>().WithMany().HasForeignKey(m => m.SenderId);

        b.HasIndex(m => new { m.ConversationId, m.CreatedAt }).HasDatabaseName("messages_conversation_created_idx");
    }
}
