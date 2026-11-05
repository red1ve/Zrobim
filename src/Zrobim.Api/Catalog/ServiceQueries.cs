using Zrobim.Api.Masters;
using Zrobim.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Zrobim.Api.Catalog;

internal static class ServiceQueries
{
    public static IQueryable<Service> VisibleInCatalog(this IQueryable<Service> services) =>
        services.Where(s => s.IsActive && s.Category.IsActive &&
            s.Master.VerificationStatus == VerificationStatus.Approved &&
            s.Master.User.IsActive && s.Master.User.Role == UserRole.Master);

    public static IQueryable<Service> InCity(this IQueryable<Service> services, string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return services;
        var pattern = EscapeSearchPattern(city.Trim());
        return services.Where(s => s.Master.City != null && EF.Functions.ILike(s.Master.City.Trim(), pattern, "!"));
    }

    public static string EscapeSearchPattern(string value) =>
        value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
}
