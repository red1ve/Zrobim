using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zrobim.Api.Bookings;
using Zrobim.Api.Users;

namespace Zrobim.Api.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");

        b.Property(n => n.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(n => n.Type).HasMaxLength(50);
        b.Property(n => n.Title).HasMaxLength(255);
        b.Property(n => n.IsRead).HasDefaultValue(false);
        b.Property(n => n.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<User>().WithMany().HasForeignKey(n => n.UserId);
        b.HasOne<Booking>().WithMany().HasForeignKey(n => n.BookingId);

        b.HasIndex(n => new { n.UserId, n.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("notifications_user_created_idx");
    }
}
