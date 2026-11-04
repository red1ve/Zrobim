namespace Zrobim.Api.Notifications;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Message { get; set; }
    public Guid? BookingId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
