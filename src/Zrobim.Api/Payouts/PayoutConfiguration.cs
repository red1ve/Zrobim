using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Payouts;

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> b)
    {
        // Trigger payouts_require_released_payment (raw SQL in InitialCreate) is the last line
        // of defence: a payout only for RELEASED / PARTIALLY_REFUNDED money, for the released amount.
        b.ToTable("payouts", t =>
        {
            t.HasCheckConstraint("payouts_amount_positive", "amount > 0");
            t.HasCheckConstraint("payouts_currency_format", "currency ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("payouts_completed_at_iff_completed",
                "(status = 'COMPLETED') = (completed_at IS NOT NULL)");
            t.HasCheckConstraint("payouts_destination_not_blank", "btrim(destination) <> ''");
            t.HasCheckConstraint("payouts_destination_not_card_number", "destination !~ '^[0-9 -]{12,}$'");
        });

        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(p => p.Amount).HasPrecision(12, 2);
        b.Property(p => p.Currency).HasColumnType("char(3)").HasDefaultValue("BYN");
        b.Property(p => p.Status).HasDefaultValue(PayoutStatus.Pending);
        b.Property(p => p.Provider).HasMaxLength(50);
        b.Property(p => p.ExternalPayoutId).HasMaxLength(255);
        b.Property(p => p.Destination).HasMaxLength(255);
        b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        b.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        // one payout per payment: a double payout is impossible
        b.HasOne(p => p.Payment).WithOne().HasForeignKey<Payout>(p => p.PaymentId);

        b.HasIndex(p => p.PaymentId).IsUnique().HasDatabaseName("payouts_payment_id_key");
        b.HasIndex(p => new { p.Provider, p.ExternalPayoutId }).IsUnique()
            .HasDatabaseName("payouts_provider_external_id_key");
        b.HasIndex(p => p.Status).HasFilter("status IN ('PENDING', 'PROCESSING', 'FAILED')")
            .HasDatabaseName("payouts_open_status_idx");
    }
}
