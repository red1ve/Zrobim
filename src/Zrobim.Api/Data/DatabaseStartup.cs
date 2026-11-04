using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Users;

namespace Zrobim.Api.Data;

internal static class DatabaseStartup
{
    private const string AdminEmail = "admin-preview@zrobim.example";
    private static readonly Guid AdminId = Guid.Parse("9f21d954-8334-48fc-959a-c4aa1fe88c71");

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

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        if (GetDemoPassword(app.Configuration, "MasterPassword") is { } masterPassword)
            await EnableMasterAsync(db, hasher, masterPassword);
        if (GetDemoPassword(app.Configuration, "AdminPassword") is { } adminPassword)
            await CreateAdminAsync(db, hasher, adminPassword, app.Logger);
    }

    private static async Task EnableMasterAsync(ZrobimDbContext db, IPasswordHasher<User> hasher, string password)
    {
        var userId = Guid.Parse("c8140b79-0b31-4888-a849-57b68d6f8a31");
        var master = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId &&
            u.Email == "catalog-master@zrobim.example" && u.Role == UserRole.Master && u.PasswordHash == "Ag==");
        if (master is null)
            return;

        var hash = hasher.HashPassword(master, password);
        await db.Users.Where(u => u.Id == userId && u.PasswordHash == "Ag==")
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.PasswordHash, hash));
    }

    private static async Task CreateAdminAsync(ZrobimDbContext db, IPasswordHasher<User> hasher, string password,
        ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Id == AdminId && u.Email == AdminEmail && u.Role == UserRole.Admin))
            return;

        var admin = new User { Id = AdminId, Email = AdminEmail, FirstName = "Администратор", Role = UserRole.Admin };
        var hash = hasher.HashPassword(admin, password);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users (id, email, password_hash, role, first_name)
            VALUES ({admin.Id}, {admin.Email}, {hash}, 'ADMIN', {admin.FirstName})
            ON CONFLICT DO NOTHING
            """);

        if (inserted == 0 && !await db.Users.AnyAsync(u => u.Id == AdminId && u.Email == AdminEmail && u.Role == UserRole.Admin))
            logger.LogWarning("The demo administrator email or ID is already in use. No role was changed.");
    }

    private static string? GetDemoPassword(IConfiguration configuration, string key)
    {
        var password = configuration["DemoCatalog:" + key];
        if (string.IsNullOrEmpty(password))
            return null;
        if (password.Length is < 8 or > 128)
            throw new InvalidOperationException($"DemoCatalog:{key} must contain between 8 and 128 characters.");
        return password;
    }
}
