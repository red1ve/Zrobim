using Zrobim.Api.Users;

namespace Zrobim.Api.Masters;

public class Master
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string? Description { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public bool ShowAddressInCatalog { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }

    /// <summary>IBAN or payment-provider token. Never a raw card number.</summary>
    public string? PayoutAccount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum VerificationStatus
{
    Pending,
    Approved,
    Rejected,
}
