using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Users;

namespace Zrobim.Api.Data;

internal static class DatabaseStartup
{
    public static async Task InitializeDemoAsync(this WebApplication app)
    {
        var migrate = app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
        var seed = app.Configuration.GetValue<bool>("DemoCatalog:SeedOnStartup");
        if (!migrate && !seed)
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ZrobimDbContext>();
        if (migrate)
            await db.Database.MigrateAsync();
        if (!seed)
            return;

        var sql = await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath, "demo", "catalog.sql"));
        await db.Database.ExecuteSqlRawAsync(sql);

        var password = app.Configuration["DemoCatalog:MasterPassword"];
        if (string.IsNullOrEmpty(password))
            return;
        if (password.Length is < 8 or > 128)
            throw new InvalidOperationException("DemoCatalog:MasterPassword must contain between 8 and 128 characters.");

        var userId = Guid.Parse("c8140b79-0b31-4888-a849-57b68d6f8a31");
        var master = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId &&
            u.Email == "catalog-master@zrobim.example" && u.Role == UserRole.Master && u.PasswordHash == "Ag==");
        if (master is null)
            return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var hash = hasher.HashPassword(master, password);
        await db.Users.Where(u => u.Id == userId && u.PasswordHash == "Ag==")
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.PasswordHash, hash));
    }
}
