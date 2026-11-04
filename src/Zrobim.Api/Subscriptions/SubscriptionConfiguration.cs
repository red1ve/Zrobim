using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Subscriptions;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("subscription_plans", t =>
            t.HasCheckConstraint("subscription_plans_price_positive", "price_monthly > 0"));

        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(p => p.Name).HasMaxLength(100);
        b.Property(p => p.PriceMonthly).HasPrecision(12, 2);
        b.Property(p => p.IsPromoted).HasDefaultValue(false);
        b.Property(p => p.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        b.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(p => p.Code).IsUnique().HasDatabaseName("subscription_plans_code_key");
    }
}

public class SubscriptionPaymentConfiguration : IEntityTypeConfiguration<SubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<SubscriptionPayment> b)
    {
        b.ToTable("subscription_payments", t =>
        {
            t.HasCheckConstraint("subscription_payments_amount_positive", "amount > 0");
            t.HasCheckConstraint("subscription_payments_currency_format", "currency ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("subscription_payments_paid_at_iff_received",
                "(status IN ('PAID', 'REFUNDED')) = (paid_at IS NOT NULL)");
        });

        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(p => p.Amount).HasPrecision(12, 2);
        b.Property(p => p.Currency).HasColumnType("char(3)").HasDefaultValue("BYN");
        b.Property(p => p.Status).HasDefaultValue(SubscriptionPaymentStatus.Pending);
        b.Property(p => p.Provider).HasMaxLength(50);
        b.Property(p => p.ExternalPaymentId).HasMaxLength(255);
        b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        b.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(p => p.Master).WithMany().HasForeignKey(p => p.MasterId);
        b.HasOne(p => p.Plan).WithMany().HasForeignKey(p => p.PlanId);

        // target of the composite FK from master_subscriptions
        b.HasAlternateKey(p => new { p.Id, p.MasterId, p.PlanId }).HasName("subscription_payments_id_master_plan_key");

        b.HasIndex(p => new { p.Provider, p.ExternalPaymentId }).IsUnique()
            .HasDatabaseName("subscription_payments_provider_external_id_key");
        b.HasIndex(p => new { p.MasterId, p.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("subscription_payments_master_created_idx");
    }
}

public class MasterSubscriptionConfiguration : IEntityTypeConfiguration<MasterSubscription>
{
    public void Configure(EntityTypeBuilder<MasterSubscription> b)
    {
        // master_subscriptions_no_overlap (exclusion constraint) and the trigger
        // master_subscriptions_require_paid_payment are created with raw SQL in InitialCreate.
        b.ToTable("master_subscriptions", t =>
        {
            t.HasCheckConstraint("master_subscriptions_price_nonneg", "price >= 0");
            // without a payment (trial / admin grant) — only free; with a payment — only paid
            t.HasCheckConstraint("master_subscriptions_free_iff_no_payment",
                "(subscription_payment_id IS NULL) = (price = 0)");
            t.HasCheckConstraint("master_subscriptions_time_order", "expires_at > starts_at");
        });

        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(s => s.Price).HasPrecision(12, 2);
        b.Property(s => s.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(s => s.Master).WithMany().HasForeignKey(s => s.MasterId);
        b.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId);
        // the payment belongs to the same master and the same plan
        b.HasOne(s => s.SubscriptionPayment).WithMany()
            .HasForeignKey(s => new { s.SubscriptionPaymentId, s.MasterId, s.PlanId })
            .HasPrincipalKey(p => new { p.Id, p.MasterId, p.PlanId })
            .HasConstraintName("master_subscriptions_payment_fk");

        b.HasIndex(s => s.SubscriptionPaymentId).IsUnique().HasDatabaseName("master_subscriptions_payment_key");
        b.HasIndex(s => new { s.MasterId, s.ExpiresAt }).HasDatabaseName("master_subscriptions_master_expires_idx");
    }
}
