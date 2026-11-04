using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Bookings;
using Zrobim.Api.Catalog;
using Zrobim.Api.Chat;
using Zrobim.Api.Disputes;
using Zrobim.Api.Favorites;
using Zrobim.Api.Masters;
using Zrobim.Api.Notifications;
using Zrobim.Api.Payments;
using Zrobim.Api.Payouts;
using Zrobim.Api.Reviews;
using Zrobim.Api.Schedule;
using Zrobim.Api.Subscriptions;
using Zrobim.Api.Users;

namespace Zrobim.Api.Data;

public class ZrobimDbContext(DbContextOptions<ZrobimDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Master> Masters => Set<Master>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();
    public DbSet<TimeBlock> TimeBlocks => Set<TimeBlock>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<MasterSubscription> MasterSubscriptions => Set<MasterSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // needed for the exclusion constraints on (uuid =, tstzrange &&)
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZrobimDbContext).Assembly);

        // Money and history are never deleted by cascade: deleting a referenced row must fail.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TouchUpdatedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchUpdatedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void TouchUpdatedAt()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = now;
        }
    }
}
