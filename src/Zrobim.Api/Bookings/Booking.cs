using Zrobim.Api.Catalog;
using Zrobim.Api.Masters;
using Zrobim.Api.Users;

namespace Zrobim.Api.Bookings;

/// <summary>State of the service (not of the money — that lives in Payment).</summary>
public class Booking
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public DateTime StartAt { get; set; }

    /// <summary>Computed by the server: StartAt + service duration.</summary>
    public DateTime EndAt { get; set; }

    /// <summary>Snapshot of the service price at request time.</summary>
    public decimal Price { get; set; }

    /// <summary>«Залог»: snapshot of 30% of the price, kept by the master on client fault.</summary>
    public decimal DepositAmount { get; set; }

    public LocationType LocationType { get; set; }
    public string? Address { get; set; }
    public BookingStatus Status { get; set; }
    public string? ClientComment { get; set; }

    /// <summary>Master must answer the request by then (12 h, but no later than start − 2 h).</summary>
    public DateTime ResponseDeadlineAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    /// <summary>The slot is held for the client until then without payment.</summary>
    public DateTime? PaymentDeadlineAt { get; set; }

    /// <summary>Set only on client fault (late cancellation / no-show).</summary>
    public DepositOutcome? DepositOutcome { get; set; }

    /// <summary>Null when cancelled by the system (timeout).</summary>
    public Guid? CancelledBy { get; set; }

    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum BookingStatus
{
    /// <summary>Request sent, waiting for the master; the slot is held for the client.</summary>
    Pending,

    /// <summary>Master accepted, waiting for the 100% payment.</summary>
    AwaitingPayment,

    /// <summary>Paid (payment HELD): fully confirmed, chat is open.</summary>
    Confirmed,

    /// <summary>Master marked the service as done; dispute window is running.</summary>
    Completed,

    /// <summary>Master marked that the client did not come; dispute window is running.</summary>
    ClientNoShow,

    Cancelled,

    /// <summary>Client opened a dispute; money stays held until an admin decides.</summary>
    Disputed,
}

public enum DepositOutcome
{
    KeptByMaster,
    WaivedByMaster,

    /// <summary>Master did not decide within 24 h — the deposit goes to the master by default.</summary>
    KeptAutomatically,
}
