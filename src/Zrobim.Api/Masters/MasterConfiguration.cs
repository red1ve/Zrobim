using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Masters;

public class MasterConfiguration : IEntityTypeConfiguration<Master>
{
    public void Configure(EntityTypeBuilder<Master> b)
    {
        b.ToTable("masters", t =>
        {
            t.HasCheckConstraint("masters_rating_range", "rating BETWEEN 0 AND 5");
            t.HasCheckConstraint("masters_reviews_count_nonneg", "reviews_count >= 0");
            t.HasCheckConstraint("masters_coordinates_pair", "(latitude IS NULL) = (longitude IS NULL)");
            t.HasCheckConstraint("masters_latitude_range", "latitude BETWEEN -90 AND 90");
            t.HasCheckConstraint("masters_longitude_range", "longitude BETWEEN -180 AND 180");
            t.HasCheckConstraint("masters_payout_account_not_blank",
                "payout_account IS NULL OR btrim(payout_account) <> ''");
            // guard against storing a raw card number (12+ digits in a row)
            t.HasCheckConstraint("masters_payout_account_not_card_number",
                "payout_account IS NULL OR payout_account !~ '^[0-9 -]{12,}$'");
        });

        b.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(m => m.City).HasMaxLength(100);
        b.Property(m => m.ShowAddressInCatalog).HasDefaultValue(false);
        b.Property(m => m.Latitude).HasPrecision(10, 7);
        b.Property(m => m.Longitude).HasPrecision(10, 7);
        b.Property(m => m.VerificationStatus).HasDefaultValue(VerificationStatus.Pending);
        b.Property(m => m.Rating).HasPrecision(3, 2).HasDefaultValue(0m);
        b.Property(m => m.ReviewsCount).HasDefaultValue(0);
        b.Property(m => m.PayoutAccount).HasMaxLength(255);
        b.Property(m => m.CreatedAt).HasDefaultValueSql("now()");
        b.Property(m => m.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(m => m.User).WithOne().HasForeignKey<Master>(m => m.UserId);

        b.HasIndex(m => m.UserId).IsUnique().HasDatabaseName("masters_user_id_key");
        b.HasIndex(m => m.VerificationStatus).HasDatabaseName("masters_verification_status_idx");
        b.HasIndex(m => m.City).HasDatabaseName("masters_city_idx");
    }
}
