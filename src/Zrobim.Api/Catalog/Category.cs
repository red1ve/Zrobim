namespace Zrobim.Api.Catalog;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
