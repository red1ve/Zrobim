using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Payments;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments", t =>
        {
            t.HasCheckConstraint("payments_amount_positive", "amount > 0");
            t.HasCheckConstraint("payments_currency_format", "currency ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("payments_expires_after_created", "expires_at > created_at");
            // money received ⇔ HELD / RELEASED / REFUNDED / PARTIALLY_REFUNDED
            t.HasCheckConstraint("payments_paid_at_iff_received",
                "(status IN ('HELD', 'RELEASED', 'REFUNDED', 'PARTIALLY_REFUNDED')) = (paid_at IS NOT NULL)");
            t.HasCheckConstraint("payments_released_at_iff_released",
                "(status IN ('RELEASED', 'PARTIALLY_REFUNDED')) = (released_at IS NOT NULL)");
            t.HasCheckConstraint("payments_refunded_at_iff_refunded",
                "(status IN ('REFUNDED', 'PARTIALLY_REFUNDED')) = (refunded_at IS NOT NULL)");
            // distributed amounts match the status and add up to the full amount
            t.HasCheckConstraint("payments_split_consistency", """
                (status IN ('PENDING', 'HELD', 'FAILED') AND released_amount = 0      AND refunded_amount = 0)
                OR (status = 'RELEASED'                  AND released_amount = amount AND refunded_amount = 0)
                OR (status = 'REFUNDED'                  AND released_amount = 0      AND refunded_amount = amount)
                OR (status = 'PARTIALLY_REFUNDED'        AND released_amount > 0      AND refunded_amount > 0
                                                         AND released_amount + refunded_amount = amount)
                """);
            t.HasCheckConstraint("payments_release_after_only_when_received",
                "release_after IS NULL OR status IN ('HELD', 'RELEASED', 'PARTIALLY_REFUNDED')");
        });

        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(p => p.Amount).HasPrecision(12, 2);
        b.Property(p => p.Currency).HasColumnType("char(3)").HasDefaultValue("BYN");
        b.Property(p => p.Status).HasDefaultValue(PaymentStatus.Pending);
        b.Property(p => p.Provider).HasMaxLength(50);
        b.Property(p => p.ExternalPaymentId).HasMaxLength(255);
        b.Property(p => p.ReleasedAmount).HasPrecision(12, 2).HasDefaultValue(0m);
        b.Property(p => p.RefundedAmount).HasPrecision(12, 2).HasDefaultValue(0m);
        b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        b.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(p => p.Booking).WithMany().HasForeignKey(p => p.BookingId);

        b.HasIndex(p => new { p.Provider, p.ExternalPaymentId }).IsUnique()
            .HasDatabaseName("payments_provider_external_id_key");
        // two indexes on booking_id, so each needs its own model name as well as a database name
        b.HasIndex(p => p.BookingId, "payments_booking_id_idx").HasDatabaseName("payments_booking_id_idx");
        // several payment attempts after FAILED, but at most one "alive" payment per booking
        b.HasIndex(p => p.BookingId, "payments_one_alive_per_booking").IsUnique()
            .HasFilter("status <> 'FAILED'").HasDatabaseName("payments_one_alive_per_booking");
        // background job: distribute held money when the timer fires / close expired carts
        b.HasIndex(p => p.ReleaseAfter).HasFilter("status = 'HELD' AND release_after IS NOT NULL")
            .HasDatabaseName("payments_auto_release_idx");
        b.HasIndex(p => p.ExpiresAt).HasFilter("status = 'PENDING'")
            .HasDatabaseName("payments_pending_expires_idx");
    }
}
