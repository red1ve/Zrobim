using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zrobim.Api.Users;

namespace Zrobim.Api.Bookings;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        // bookings_no_overlap (exclusion constraint against double booking) is created
        // with raw SQL in the InitialCreate migration: EF cannot model exclusion constraints.
        b.ToTable("bookings", t =>
        {
            t.HasCheckConstraint("bookings_time_order", "end_at > start_at");
            t.HasCheckConstraint("bookings_price_positive", "price > 0");
            t.HasCheckConstraint("bookings_deposit_range", "deposit_amount > 0 AND deposit_amount <= price");
            t.HasCheckConstraint("bookings_response_deadline_before_start", "response_deadline_at <= start_at");
            t.HasCheckConstraint("bookings_payment_deadline_iff_accepted",
                "(accepted_at IS NULL) = (payment_deadline_at IS NULL)");
            t.HasCheckConstraint("bookings_accepted_for_later_statuses",
                "status IN ('PENDING', 'CANCELLED') OR accepted_at IS NOT NULL");
            t.HasCheckConstraint("bookings_deposit_outcome_only_on_client_fault",
                "deposit_outcome IS NULL OR status IN ('CANCELLED', 'CLIENT_NO_SHOW', 'DISPUTED')");
            t.HasCheckConstraint("bookings_client_place_needs_address",
                "location_type <> 'CLIENT_PLACE' OR (address IS NOT NULL AND btrim(address) <> '')");
            t.HasCheckConstraint("bookings_cancelled_at_iff_cancelled",
                "(status = 'CANCELLED') = (cancelled_at IS NOT NULL)");
            t.HasCheckConstraint("bookings_cancelled_by_only_when_cancelled",
                "cancelled_by IS NULL OR status = 'CANCELLED'");
            t.HasCheckConstraint("bookings_completed_has_timestamp",
                "status <> 'COMPLETED' OR completed_at IS NOT NULL");
        });

        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Price).HasPrecision(12, 2);
        b.Property(x => x.DepositAmount).HasPrecision(12, 2);
        b.Property(x => x.Status).HasDefaultValue(BookingStatus.Pending);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
        b.HasOne(x => x.Master).WithMany().HasForeignKey(x => x.MasterId);
        // the service must exist AND belong to this booking's master
        b.HasOne(x => x.Service).WithMany()
            .HasForeignKey(x => new { x.ServiceId, x.MasterId })
            .HasPrincipalKey(s => new { s.Id, s.MasterId })
            .HasConstraintName("bookings_service_master_fk");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy);

        // target of the composite FK from reviews
        b.HasAlternateKey(x => new { x.Id, x.ClientId, x.MasterId }).HasName("bookings_id_client_master_key");

        b.HasIndex(x => new { x.ClientId, x.StartAt }).IsDescending(false, true)
            .HasDatabaseName("bookings_client_start_idx");
        b.HasIndex(x => new { x.MasterId, x.StartAt }).HasDatabaseName("bookings_master_start_idx");

        // background job: master did not answer / client did not pay / nobody marked the result
        b.HasIndex(x => x.ResponseDeadlineAt).HasFilter("status = 'PENDING'")
            .HasDatabaseName("bookings_pending_response_idx");
        b.HasIndex(x => x.PaymentDeadlineAt).HasFilter("status = 'AWAITING_PAYMENT'")
            .HasDatabaseName("bookings_awaiting_payment_idx");
        b.HasIndex(x => x.EndAt).HasFilter("status = 'CONFIRMED'")
            .HasDatabaseName("bookings_confirmed_end_idx");
    }
}
