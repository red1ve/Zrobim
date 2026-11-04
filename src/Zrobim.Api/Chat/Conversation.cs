using Zrobim.Api.Masters;

namespace Zrobim.Api.Chat;

/// <summary>
/// Private chat of a client and a master (like Kufar): one per client–master pair,
/// opened when their first booking is paid (CONFIRMED). No money flows here.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>Text, one photo, or both. The photo itself lives in file storage; only its key is stored.</summary>
public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public string? Text { get; set; }
    public string? AttachmentKey { get; set; }
    public string? AttachmentContentType { get; set; }
    public int? AttachmentSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
