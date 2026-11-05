using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Data;
using Zrobim.Api.Schedule;

namespace Zrobim.Api.Catalog;

[ApiController]
[Route("api/catalog")]
public class CatalogController(ZrobimDbContext db) : ControllerBase
{
    private IQueryable<Service> VisibleServices => db.Services.VisibleInCatalog();

    [HttpGet("categories")]
    public Task<List<CatalogCategory>> GetCategories(CancellationToken ct,
        [FromQuery, MaxLength(100)] string? city = null)
    {
        var services = VisibleServices.InCity(city);
        var categories = db.Categories.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(city))
            categories = categories.Where(c => services.Any(s => s.CategoryId == c.Id));

        return categories.OrderBy(c => c.Name)
            .Select(c => new CatalogCategory(c.Id, c.Name,
                services.Where(s => s.CategoryId == c.Id).Select(s => s.MasterId).Distinct().Count(),
                services.Count(s => s.CategoryId == c.Id)))
            .ToListAsync(ct);
    }

    [HttpGet("cities")]
    public Task<List<string>> GetCities(CancellationToken ct) =>
        VisibleServices.Where(s => s.Master.City != null && s.Master.City.Trim() != "")
            .Select(s => s.Master.City!.Trim())
            .GroupBy(city => city.ToLower())
            .OrderBy(group => group.Key)
            .Select(group => group.Min()!)
            .ToListAsync(ct);

    [HttpGet("search")]
    public async Task<CatalogSearchPage> Search(
        [FromQuery, MaxLength(100)] string? q,
        [FromQuery, MaxLength(100)] string? city,
        CancellationToken ct,
        [FromQuery, Range(1, 1000)] int page = 1)
    {
        const int pageSize = 20;
        var services = VisibleServices.InCity(city);

        foreach (var term in (q ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = "%" + ServiceQueries.EscapeSearchPattern(term) + "%";
            services = services.Where(s => EF.Functions.ILike(
                s.Name + " " + s.Category.Name + " " + s.Master.User.FirstName + " " + (s.Master.User.LastName ?? ""),
                pattern, "!"));
        }

        var results = await services
            .OrderByDescending(s => s.Master.Rating)
            .ThenBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(s => new CatalogSearchResult(s.Category.Name,
                new CatalogMaster(s.MasterId, s.Master.User.FirstName, s.Master.User.LastName,
                    s.Master.City, s.Master.Description, s.Master.Rating, s.Master.ReviewsCount,
                    s.Master.ShowAddressInCatalog && s.LocationType == LocationType.MasterPlace ? s.Master.Address : null),
                new CatalogService(s.Id, s.Name, s.Description, s.Price, "BYN", s.DurationMinutes, s.LocationType)))
            .ToListAsync(ct);

        return new CatalogSearchPage(results.Take(pageSize).ToArray(), page < 1000 && results.Count > pageSize);
    }


    [HttpGet("categories/{categoryId:guid}/masters")]
    public Task<List<CatalogMaster>> GetMasters(Guid categoryId, CancellationToken ct,
        [FromQuery, MaxLength(100)] string? city = null)
    {
        var services = VisibleServices.InCity(city).Where(s => s.CategoryId == categoryId);

        return db.Masters
            .Where(m => services.Any(s => s.MasterId == m.Id))
            .OrderByDescending(m => m.Rating)
            .ThenBy(m => m.User.FirstName)
            .ThenBy(m => m.Id)
            .Select(m => new CatalogMaster(m.Id, m.User.FirstName, m.User.LastName,
                m.City, m.Description, m.Rating, m.ReviewsCount,
                m.ShowAddressInCatalog && services.Any(s => s.MasterId == m.Id && s.LocationType == LocationType.MasterPlace)
                    ? m.Address : null))
            .ToListAsync(ct);
    }

    [HttpGet("categories/{categoryId:guid}/masters/{masterId:guid}/services")]
    public Task<List<CatalogService>> GetServices(Guid categoryId, Guid masterId, CancellationToken ct) =>
        GetServicesForMaster(VisibleServices.Where(s => s.CategoryId == categoryId && s.MasterId == masterId), ct);

    [HttpGet("masters/{masterId:guid}/services")]
    public Task<List<CatalogService>> GetMasterServices(Guid masterId, CancellationToken ct) =>
        GetServicesForMaster(VisibleServices.Where(s => s.MasterId == masterId), ct);

    private static Task<List<CatalogService>> GetServicesForMaster(IQueryable<Service> services, CancellationToken ct) =>
        services
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new CatalogService(s.Id, s.Name, s.Description,
                s.Price, "BYN", s.DurationMinutes, s.LocationType))
            .ToListAsync(ct);

    [HttpGet("services/{serviceId:guid}/availability")]
    public async Task<ActionResult<ServiceAvailability>> GetAvailability(
        Guid serviceId, [FromServices] AvailabilityService availability, CancellationToken ct)
    {
        var service = await VisibleServices
            .Where(s => s.Id == serviceId)
            .Select(s => new { s.MasterId, s.DurationMinutes })
            .SingleOrDefaultAsync(ct);

        if (service is null)
            return NotFound();

        return await availability.GetAsync(service.MasterId, service.DurationMinutes, ct);
    }
}

public sealed record CatalogCategory(Guid Id, string Name, int MasterCount = 0, int ServiceCount = 0);

public sealed record CatalogMaster(Guid Id, string FirstName, string? LastName,
    string? City, string? Description, decimal Rating, int ReviewsCount, string? PublicAddress);

public sealed record CatalogService(Guid Id, string Name, string? Description,
    decimal Price, string Currency, int DurationMinutes, LocationType LocationType);

public sealed record CatalogSearchResult(string CategoryName, CatalogMaster Master, CatalogService Service);

public sealed record CatalogSearchPage(IReadOnlyList<CatalogSearchResult> Items, bool HasMore);
