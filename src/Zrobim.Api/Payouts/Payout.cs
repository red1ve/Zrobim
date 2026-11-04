using Zrobim.Api.Payments;

namespace Zrobim.Api.Payouts;

/// <summary>
/// Transfer of already released money to the master (flow №2: ЗРОБИМ → MASTER).
/// Created only together with HELD → RELEASED / PARTIALLY_REFUNDED. The master is reached
/// through payment → booking → master.
/// </summary>
public class Payout
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    /// <summary>Equals the payment's released amount (100% or the deposit).</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "BYN";
    public PayoutStatus Status { get; set; }
    public string Provider { get; set; } = null!;
    public string? ExternalPayoutId { get; set; }

    /// <summary>Snapshot of the master's payout account at payout time.</summary>
    public string Destination { get; set; } = null!;

    public string? FailureReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum PayoutStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}
