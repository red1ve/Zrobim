using Zrobim.Api.Masters;

namespace Zrobim.Api.Catalog;

public class Service
{
    public Guid Id { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public LocationType LocationType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum LocationType
{
    MasterPlace,
    ClientPlace,
}
