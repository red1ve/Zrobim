using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Data;

namespace Zrobim.Api.Masters;

internal static class MasterWriteLock
{
    // Call inside the transaction that checks and changes the master's availability.
    public static Task<int> AcquireAsync(ZrobimDbContext db, Guid masterId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM masters WHERE id = {masterId} FOR UPDATE", ct);
}
