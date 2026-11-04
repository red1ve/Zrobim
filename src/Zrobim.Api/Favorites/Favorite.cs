using Zrobim.Api.Masters;

namespace Zrobim.Api.Favorites;

public class Favorite
{
    public Guid ClientId { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
