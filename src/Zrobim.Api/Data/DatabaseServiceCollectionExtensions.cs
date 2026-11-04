using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Zrobim.Api.Bookings;
using Zrobim.Api.Catalog;
using Zrobim.Api.Disputes;
using Zrobim.Api.Masters;
using Zrobim.Api.Payments;
using Zrobim.Api.Payouts;
using Zrobim.Api.Subscriptions;
using Zrobim.Api.Users;

namespace Zrobim.Api.Data;

public static class DatabaseServiceCollectionExtensions
{
    // One shared instance: EF compares DbContext options per request, and a new translator
    // each time would make EF build a new internal service provider for every context
    // (it throws ManyServiceProvidersCreatedWarning after 20).
    private static readonly UpperSnakeCaseNameTranslator EnumLabels = new();

    public static IServiceCollection AddZrobimDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? ConnectionStringFromUrl(configuration["Database:Url"]);

        services.AddDbContext<ZrobimDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MapZrobimEnums())
            .UseSnakeCaseNamingConvention());

        return services;
    }

    private static string ConnectionStringFromUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "postgresql" ||
            string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath.Length <= 1 || uri.Query.Length != 0)
            throw new InvalidOperationException("Set ConnectionStrings__Default or a valid Database__Url.");

        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || credentials[0].Length == 0 || credentials[1].Length == 0)
            throw new InvalidOperationException("Database__Url must include a database username and password.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath[1..]),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        }.ConnectionString;
    }


    /// <summary>C# enums ⇄ PostgreSQL enum types with UPPER_SNAKE_CASE labels.</summary>
    private static void MapZrobimEnums(this NpgsqlDbContextOptionsBuilder npgsql)
    {
        npgsql.MapEnum<UserRole>("user_role", nameTranslator: EnumLabels);
        npgsql.MapEnum<VerificationStatus>("verification_status", nameTranslator: EnumLabels);
        npgsql.MapEnum<LocationType>("location_type", nameTranslator: EnumLabels);
        npgsql.MapEnum<BookingStatus>("booking_status", nameTranslator: EnumLabels);
        npgsql.MapEnum<DepositOutcome>("deposit_outcome", nameTranslator: EnumLabels);
        npgsql.MapEnum<PaymentStatus>("payment_status", nameTranslator: EnumLabels);
        npgsql.MapEnum<PayoutStatus>("payout_status", nameTranslator: EnumLabels);
        npgsql.MapEnum<DisputeReason>("dispute_reason", nameTranslator: EnumLabels);
        npgsql.MapEnum<DisputeStatus>("dispute_status", nameTranslator: EnumLabels);
        npgsql.MapEnum<DisputeResolution>("dispute_resolution", nameTranslator: EnumLabels);
        npgsql.MapEnum<SubscriptionPlanCode>("subscription_plan_code", nameTranslator: EnumLabels);
        npgsql.MapEnum<SubscriptionPaymentStatus>("subscription_payment_status", nameTranslator: EnumLabels);
    }
}
