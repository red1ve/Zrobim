using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zrobim.Api.Masters;
using Zrobim.Api.Users;

namespace Zrobim.Api.Reviews;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("reviews", t => t.HasCheckConstraint("reviews_rating_range", "rating BETWEEN 1 AND 5"));

        b.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(r => r.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<User>().WithMany().HasForeignKey(r => r.ClientId);
        b.HasOne<Master>().WithMany().HasForeignKey(r => r.MasterId);
        // the review's client and master must be the booking's client and master
        b.HasOne(r => r.Booking).WithMany()
            .HasForeignKey(r => new { r.BookingId, r.ClientId, r.MasterId })
            .HasPrincipalKey(x => new { x.Id, x.ClientId, x.MasterId })
            .HasConstraintName("reviews_booking_participants_fk");

        b.HasIndex(r => r.BookingId).IsUnique().HasDatabaseName("reviews_booking_id_key");
        b.HasIndex(r => new { r.MasterId, r.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("reviews_master_created_idx");
    }
}
