using Zrobim.Api.Masters;
using Zrobim.Api.Users;

namespace Zrobim.Api.Catalog;

internal static class ServiceQueries
{
    public static IQueryable<Service> VisibleInCatalog(this IQueryable<Service> services) =>
        services.Where(s => s.IsActive && s.Category.IsActive &&
            s.Master.VerificationStatus == VerificationStatus.Approved &&
            s.Master.User.IsActive && s.Master.User.Role == UserRole.Master);
}
