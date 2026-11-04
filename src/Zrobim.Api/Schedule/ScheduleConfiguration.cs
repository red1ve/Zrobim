using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Schedule;

public class WorkingHoursConfiguration : IEntityTypeConfiguration<WorkingHours>
{
    public void Configure(EntityTypeBuilder<WorkingHours> b)
    {
        b.ToTable("master_working_hours", t =>
        {
            t.HasCheckConstraint("master_working_hours_day_range", "day_of_week BETWEEN 1 AND 7");
            t.HasCheckConstraint("master_working_hours_time_order", "end_time > start_time");
        });

        b.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");

        b.HasOne(w => w.Master).WithMany().HasForeignKey(w => w.MasterId);

        b.HasIndex(w => new { w.MasterId, w.DayOfWeek }).IsUnique()
            .HasDatabaseName("master_working_hours_master_day_key");
    }
}

public class TimeBlockConfiguration : IEntityTypeConfiguration<TimeBlock>
{
    public void Configure(EntityTypeBuilder<TimeBlock> b)
    {
        b.ToTable("master_time_blocks", t =>
            t.HasCheckConstraint("master_time_blocks_time_order", "end_at > start_at"));

        b.Property(tb => tb.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(tb => tb.Reason).HasMaxLength(255);

        b.HasOne(tb => tb.Master).WithMany().HasForeignKey(tb => tb.MasterId);

        b.HasIndex(tb => new { tb.MasterId, tb.StartAt }).HasDatabaseName("master_time_blocks_master_start_idx");
    }
}
