using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zrobim.Api.Users;

namespace Zrobim.Api.Disputes;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> b)
    {
        b.ToTable("disputes", t => t.HasCheckConstraint("disputes_resolution_consistency", """
            (status = 'OPEN'     AND resolution IS NULL     AND resolved_by IS NULL     AND resolved_at IS NULL)
            OR (status = 'RESOLVED' AND resolution IS NOT NULL AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL)
            """));

        b.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(d => d.Status).HasDefaultValue(DisputeStatus.Open);
        b.Property(d => d.CreatedAt).HasDefaultValueSql("now()");

        // at most one dispute per booking
        b.HasOne(d => d.Booking).WithOne().HasForeignKey<Dispute>(d => d.BookingId);
        b.HasOne<User>().WithMany().HasForeignKey(d => d.OpenedBy);
        b.HasOne<User>().WithMany().HasForeignKey(d => d.ResolvedBy);

        b.HasIndex(d => d.BookingId).IsUnique().HasDatabaseName("disputes_booking_id_key");
        b.HasIndex(d => d.CreatedAt).HasFilter("status = 'OPEN'").HasDatabaseName("disputes_open_created_idx");
    }
}
