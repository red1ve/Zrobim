using Zrobim.Api.Bookings;

namespace Zrobim.Api.Reviews;

public class Review
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid ClientId { get; set; }
    public Guid MasterId { get; set; }

    /// <summary>1..5</summary>
    public int Rating { get; set; }

    public string? Text { get; set; }
    public DateTime CreatedAt { get; set; }
}
