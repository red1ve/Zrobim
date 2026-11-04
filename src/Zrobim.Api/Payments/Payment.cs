using Zrobim.Api.Bookings;

namespace Zrobim.Api.Payments;

/// <summary>
/// Client money for a booking (flow №2: CLIENT → ЗРОБИМ). Client and master are reached
/// through the booking. <see cref="Id"/> is sent to the provider as order id / idempotency key.
/// Status changes only via PaymentService after a provider webhook — never from the mobile app.
/// </summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    /// <summary>Snapshot of the booking price. Never taken from the client request.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "BYN";
    public PaymentStatus Status { get; set; }
    public string Provider { get; set; } = null!;
    public string? ExternalPaymentId { get; set; }

    /// <summary>The 20-minute «cart»: starts when the client presses «Оплатить».</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the held money is distributed: after COMPLETED — all to the master,
    /// after client fault — deposit to the master, the rest back to the client.
    /// Null = not scheduled (a dispute is open, or the money waits for a full refund).
    /// </summary>
    public DateTime? ReleaseAfter { get; set; }

    public decimal ReleasedAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Money received and held by the platform.</summary>
    public DateTime? PaidAt { get; set; }

    public DateTime? ReleasedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum PaymentStatus
{
    /// <summary>«Cart»: the client pressed «Оплатить», the provider has not confirmed yet.</summary>
    Pending,

    /// <summary>Money received by the platform and held.</summary>
    Held,

    /// <summary>The full amount goes to the master.</summary>
    Released,

    /// <summary>The full amount goes back to the client.</summary>
    Refunded,

    /// <summary>Deposit to the master, the rest back to the client.</summary>
    PartiallyRefunded,

    /// <summary>Not paid: provider declined or the cart expired.</summary>
    Failed,
}
