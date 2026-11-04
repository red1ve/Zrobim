using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;

namespace Zrobim.Api.Bookings;

[ApiController]
[Route("api")]
public class BookingCancellationController(ZrobimDbContext db, TimeProvider time) : ControllerBase
{
    [Authorize(Roles = Roles.Client)]
    [HttpPost("bookings/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        var now = time.GetUtcNow().UtcDateTime;
        var owned = db.Bookings.Where(b => b.Id == id && b.ClientId == actorId);
        return CancelOwned(owned, owned.OpenForClientCancellation(now), actorId, now, "CLIENT_CANCELLED", ct);
    }

    [Authorize(Roles = Roles.Master)]
    [HttpPost("master/bookings/{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        var now = time.GetUtcNow().UtcDateTime;
        var owned = db.Bookings.Where(b => b.Id == id && b.Master.UserId == actorId);
        var eligible = owned.Where(b => b.Status == BookingStatus.Pending && b.ResponseDeadlineAt > now &&
            b.StartAt > now);
        return CancelOwned(owned, eligible, actorId, now, "MASTER_DECLINED", ct);
    }

    private async Task<IActionResult> CancelOwned(IQueryable<Booking> owned, IQueryable<Booking> eligible,
        Guid actorId, DateTime now, string reason, CancellationToken ct)
    {
        var changed = await eligible.WithoutActivePayment(db).CancelAsync(now, actorId, _ => reason, ct);
        if (changed == 1)
            return NoContent();

        var existing = await owned.Select(b => new { b.Status, b.CancelledBy, b.CancellationReason })
            .SingleOrDefaultAsync(ct);
        if (existing is null)
            return NotFound();

        if (existing.Status == BookingStatus.Cancelled && existing.CancelledBy == actorId &&
            existing.CancellationReason == reason)
            return NoContent();

        return Problem(statusCode: StatusCodes.Status409Conflict, title: "Эту заявку уже нельзя отменить.");
    }
}
