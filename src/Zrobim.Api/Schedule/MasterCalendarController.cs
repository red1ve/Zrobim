using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Bookings;
using Zrobim.Api.Data;

namespace Zrobim.Api.Schedule;

[ApiController]
[Authorize(Roles = Roles.Master)]
[Route("api/master/calendar")]
public class MasterCalendarController(ZrobimDbContext db, TimeProvider time) : ControllerBase
{
    private const int DaysPerWeek = 7;

    [HttpGet]
    public async Task<ActionResult<MasterCalendarResponse>> GetWeek(CancellationToken ct,
        [FromQuery, Range(-52, 52)] int weekOffset = 0)
    {
        var userId = User.GetUserId();
        var masterId = await db.Masters.Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.Id).SingleOrDefaultAsync(ct);
        if (masterId is null)
            return NotFound();

        var today = AvailabilityService.LocalDate(time.GetUtcNow());
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % DaysPerWeek;
        var monday = today.AddDays(weekOffset * DaysPerWeek - daysSinceMonday);
        var rangeStart = AvailabilityService.ToUtc(monday, TimeOnly.MinValue);
        var rangeEnd = AvailabilityService.ToUtc(monday.AddDays(DaysPerWeek), TimeOnly.MinValue);

        var hours = await db.WorkingHours.AsNoTracking().Where(w => w.MasterId == masterId)
            .ToDictionaryAsync(w => w.DayOfWeek, ct);
        var blocks = await db.TimeBlocks
            .Where(b => b.MasterId == masterId && b.StartAt < rangeEnd && rangeStart < b.EndAt)
            .Select(b => new CalendarEntry(b.StartAt, b.EndAt, null, null, "Время заблокировано", b.Reason))
            .ToListAsync(ct);
        var bookings = await db.Bookings
            .Where(b => b.MasterId == masterId && b.Status != BookingStatus.Cancelled &&
                b.StartAt < rangeEnd && rangeStart < b.EndAt)
            .Select(b => new CalendarEntry(b.StartAt, b.EndAt, b.Id, b.Status, b.Service.Name,
                b.Client.FirstName + (b.Client.LastName == null ? "" : " " + b.Client.LastName)))
            .ToListAsync(ct);

        var entries = blocks.Concat(bookings).OrderBy(e => e.StartAt).ThenBy(e => e.EndAt)
            .ThenBy(e => e.Title).ToArray();
        List<CalendarDay> days = [];
        for (var day = 0; day < DaysPerWeek; day++)
        {
            var date = monday.AddDays(day);
            var start = AvailabilityService.ToUtc(date, TimeOnly.MinValue);
            var end = AvailabilityService.ToUtc(date.AddDays(1), TimeOnly.MinValue);
            hours.TryGetValue(day + 1, out var workingDay);
            var events = entries.Where(e => e.StartAt < end && start < e.EndAt)
                .Select(e => e with { StartAt = e.StartAt < start ? start : e.StartAt, EndAt = e.EndAt > end ? end : e.EndAt })
                .ToArray();
            days.Add(new CalendarDay(date, end, workingDay?.StartTime, workingDay?.EndTime, events));
        }

        return new MasterCalendarResponse(today, days);
    }
}

public sealed record MasterCalendarResponse(DateOnly Today, IReadOnlyList<CalendarDay> Days);

public sealed record CalendarDay(DateOnly Date, DateTime EndAt, TimeOnly? WorkStart, TimeOnly? WorkEnd,
    IReadOnlyList<CalendarEntry> Entries);

public sealed record CalendarEntry(DateTime StartAt, DateTime EndAt, Guid? BookingId, BookingStatus? Status,
    string Title, string? Detail);
