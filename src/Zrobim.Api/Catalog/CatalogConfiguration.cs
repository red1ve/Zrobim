using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Catalog;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");

        b.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.ImageUrl).HasMaxLength(500);
        b.Property(c => c.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        b.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(c => c.Name).IsUnique().HasDatabaseName("categories_name_key");
    }
}

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("services", t =>
        {
            t.HasCheckConstraint("services_price_positive", "price > 0");
            t.HasCheckConstraint("services_duration_range", "duration_minutes > 0 AND duration_minutes <= 1440");
        });

        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(s => s.Name).HasMaxLength(150);
        b.Property(s => s.Price).HasPrecision(12, 2);
        b.Property(s => s.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        b.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(s => s.Master).WithMany().HasForeignKey(s => s.MasterId);
        b.HasOne(s => s.Category).WithMany().HasForeignKey(s => s.CategoryId);

        // target of the composite FK from bookings: the service must belong to the booking's master
        b.HasAlternateKey(s => new { s.Id, s.MasterId }).HasName("services_id_master_key");

        b.HasIndex(s => s.MasterId).HasDatabaseName("services_master_id_idx");
        b.HasIndex(s => s.CategoryId).HasFilter("is_active").HasDatabaseName("services_category_active_idx");
    }
}
