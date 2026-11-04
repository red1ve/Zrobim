using Zrobim.Api.Masters;

namespace Zrobim.Api.Schedule;

/// <summary>Weekly template of a master's working time. No row for a day = day off.</summary>
public class WorkingHours
{
    public Guid Id { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;

    /// <summary>1 = Monday … 7 = Sunday.</summary>
    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

/// <summary>One-off period when a master is unavailable.</summary>
public class TimeBlock
{
    public Guid Id { get; set; }
    public Guid MasterId { get; set; }
    public Master Master { get; set; } = null!;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? Reason { get; set; }
}
