using Zrobim.Api.Masters;

namespace Zrobim.Api.Subscriptions;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public SubscriptionPlanCode Code { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Current price; changes without migrations. Purchases keep their own snapshot.</summary>
    public decimal PriceMonthly { get; set; }

    /// <summary>Search promotion is driven by this flag, never by <see cref="Code"/>.</summary>
    public bool IsPromoted { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Master's payment for a subscription (flow №1: MASTER → ЗРОБИМ, platform revenue).</summary>
public class SubscriptionPayment
{
    public Guid Id { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    /// <summary>Snapshot of the plan price.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "BYN";
    public SubscriptionPaymentStatus Status { get; set; }
    public string Provider { get; set; } = null!;
    public string? ExternalPaymentId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// A granted access period. No status: active ⇔ CancelledAt is null and StartsAt ≤ now &lt; ExpiresAt.
/// Renewal and plan change add a new row; history is never deleted.
/// </summary>
public class MasterSubscription
{
    public Guid Id { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    /// <summary>Null only for a free trial or an admin grant (then <see cref="Price"/> is 0).</summary>
    public Guid? SubscriptionPaymentId { get; set; }

    public SubscriptionPayment? SubscriptionPayment { get; set; }

    /// <summary>Price snapshot at purchase time.</summary>
    public decimal Price { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum SubscriptionPlanCode
{
    Basic,
    Plus,
}

public enum SubscriptionPaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
}
