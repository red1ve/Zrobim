using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zrobim.Api.Auth;
using Zrobim.Api.Catalog;
using Zrobim.Api.Data;

namespace Zrobim.Api.Favorites;

[ApiController]
[Authorize(Roles = Roles.Client)]
[Route("api/favorites")]
public class FavoritesController(ZrobimDbContext db) : ControllerBase
{
    private IQueryable<Favorite> OwnFavorites(Guid clientId) =>
        db.Favorites.Where(f => f.ClientId == clientId);

    [HttpGet]
    public async Task<FavoritePage> GetPage(CancellationToken ct, [FromQuery, Range(1, 1000)] int page = 1)
    {
        const int pageSize = 20;
        var visible = db.Services.VisibleInCatalog();
        var items = await OwnFavorites(User.GetUserId())
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.MasterId)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(f => new FavoriteResponse(f.MasterId,
                visible.Any(s => s.MasterId == f.MasterId)
                    ? new CatalogMaster(f.MasterId, f.Master.User.FirstName, f.Master.User.LastName,
                        f.Master.City, f.Master.Description, f.Master.Rating, f.Master.ReviewsCount,
                        f.Master.ShowAddressInCatalog &&
                        visible.Any(s => s.MasterId == f.MasterId && s.LocationType == LocationType.MasterPlace)
                            ? f.Master.Address : null)
                    : null))
            .ToListAsync(ct);

        return new FavoritePage(items.Take(pageSize).ToArray(), page < 1000 && items.Count > pageSize);
    }

    [HttpGet("{masterId:guid}")]
    public async Task<FavoriteStatus> GetStatus(Guid masterId, CancellationToken ct) =>
        new(await OwnFavorites(User.GetUserId()).AnyAsync(f => f.MasterId == masterId, ct));

    [HttpPut("{masterId:guid}")]
    public async Task<IActionResult> Save(Guid masterId, CancellationToken ct)
    {
        var clientId = User.GetUserId();
        if (await OwnFavorites(clientId).AnyAsync(f => f.MasterId == masterId, ct))
            return NoContent();
        if (!await db.Services.VisibleInCatalog().AnyAsync(s => s.MasterId == masterId, ct))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Этот мастер сейчас недоступен.");

        db.Favorites.Add(new Favorite { ClientId = clientId, MasterId = masterId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request already saved this pair.
        }
        return NoContent();
    }

    [HttpDelete("{masterId:guid}")]
    public async Task<IActionResult> Remove(Guid masterId, CancellationToken ct)
    {
        await OwnFavorites(User.GetUserId()).Where(f => f.MasterId == masterId).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}

public sealed record FavoriteStatus(bool IsFavorite);
public sealed record FavoriteResponse(Guid MasterId, CatalogMaster? Master);
public sealed record FavoritePage(IReadOnlyList<FavoriteResponse> Items, bool HasMore);
