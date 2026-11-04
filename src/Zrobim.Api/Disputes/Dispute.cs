using Zrobim.Api.Bookings;

namespace Zrobim.Api.Disputes;

/// <summary>A client's dispute about a booking. While it is open, the payment stays HELD.</summary>
public class Dispute
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid OpenedBy { get; set; }
    public DisputeReason Reason { get; set; }
    public string? Description { get; set; }
    public DisputeStatus Status { get; set; }
    public DisputeResolution? Resolution { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolutionComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum DisputeReason
{
    ServiceNotProvided,
    MasterNoShow,
    PoorQuality,
    Other,
}

public enum DisputeStatus
{
    Open,
    Resolved,
}

public enum DisputeResolution
{
    /// <summary>Everything to the master.</summary>
    ReleaseToMaster,

    /// <summary>Everything back to the client.</summary>
    RefundToClient,

    /// <summary>Deposit to the master, the rest back to the client.</summary>
    DepositToMaster,
}
