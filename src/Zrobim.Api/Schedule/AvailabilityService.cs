using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Bookings;
using Zrobim.Api.Data;

namespace Zrobim.Api.Schedule;

public sealed class AvailabilityService(ZrobimDbContext db, TimeProvider time)
{
    private const int DaysToShow = 7;
    private const int SlotStepMinutes = 30;
    private static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(2);
    private static readonly TimeZoneInfo ScheduleZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Minsk");

    public async Task<ServiceAvailability> GetAsync(Guid masterId, int durationMinutes, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var earliestStart = now.UtcDateTime + MinimumNotice;
        var firstDate = LocalDate(now);
        var endDate = firstDate.AddDays(DaysToShow);
        var rangeEnd = ToUtc(endDate, TimeOnly.MinValue);

        var workingDays = await db.WorkingHours
            .Where(w => w.MasterId == masterId)
            .ToDictionaryAsync(w => w.DayOfWeek, ct);

        var blocks = db.TimeBlocks
            .Where(b => b.MasterId == masterId && b.StartAt < rangeEnd && b.EndAt > earliestStart)
            .Select(b => new { b.StartAt, b.EndAt });
        var bookings = db.Bookings.HoldingTime()
            .Where(b => b.MasterId == masterId && b.StartAt < rangeEnd && b.EndAt > earliestStart)
            .Select(b => new { b.StartAt, b.EndAt });
        var occupied = await blocks.Concat(bookings).ToListAsync(ct);

        List<AvailableSlot> slots = [];
        for (var date = firstDate; date < endDate; date = date.AddDays(1))
        {
            var dayOfWeek = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            if (!workingDays.TryGetValue(dayOfWeek, out var hours))
                continue;

            var workEnd = ToUtc(date, hours.EndTime);
            var lastStart = workEnd.AddMinutes(-durationMinutes);
            for (var start = ToUtc(date, hours.StartTime); start <= lastStart; start = start.AddMinutes(SlotStepMinutes))
            {
                var end = start.AddMinutes(durationMinutes);
                if (start > earliestStart && !occupied.Any(b => b.StartAt < end && start < b.EndAt))
                    slots.Add(new AvailableSlot(start, end));
            }
        }

        return new ServiceAvailability(ScheduleZone.Id, slots);
    }

    internal static DateTime GetResponseDeadline(DateTime now, DateTime start)
    {
        var limit = now.AddHours(12);
        var cutoff = start - MinimumNotice;
        return limit < cutoff ? limit : cutoff;
    }


    internal static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(local, ScheduleZone);

    internal static DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, ScheduleZone).DateTime);

    internal static DateTime ToUtc(DateOnly date, TimeOnly time) => ToUtc(date.ToDateTime(time));
}

public sealed record ServiceAvailability(string TimeZone, IReadOnlyList<AvailableSlot> Slots);

public sealed record AvailableSlot(DateTime StartAt, DateTime EndAt);
