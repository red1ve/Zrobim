using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;
using Zrobim.Api.Masters;

namespace Zrobim.Api.Bookings;

[ApiController]
[Authorize(Roles = Roles.Master)]
[Route("api/master/bookings")]
public class MasterBookingsController(ZrobimDbContext db, TimeProvider time) : ControllerBase
{
    private IQueryable<Booking> OwnBookings(Guid userId) =>
        db.Bookings.Where(b => b.Master.UserId == userId);

    [HttpGet]
    public Task<List<MasterBookingResponse>> GetIncoming(CancellationToken ct) =>
        OwnBookings(User.GetUserId())
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Select(b => new MasterBookingResponse(b.Id, b.Service.Name,
                b.Client.FirstName, b.Client.LastName, b.Status, b.StartAt, b.EndAt,
                b.Price, "BYN", b.ResponseDeadlineAt, b.Address, b.ClientComment, b.CancellationReason))
            .ToListAsync(ct);

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var booking = OwnBookings(User.GetUserId()).Where(b => b.Id == id);
        var now = time.GetUtcNow().UtcDateTime;
        var paymentLimit = now.AddHours(6);

        // Check ownership, state and deadline as part of the same update.
        var changed = await booking
            .Where(b => b.Status == BookingStatus.Pending && b.ResponseDeadlineAt > now &&
                b.StartAt > now && b.Master.VerificationStatus == VerificationStatus.Approved)
            .ExecuteUpdateAsync(set => set
                .SetProperty(b => b.Status, BookingStatus.AwaitingPayment)
                .SetProperty(b => b.AcceptedAt, now)
                .SetProperty(b => b.PaymentDeadlineAt, b => b.StartAt < paymentLimit ? b.StartAt : paymentLimit)
                .SetProperty(b => b.UpdatedAt, now), ct);

        if (changed == 1)
            return NoContent();

        var status = await booking.Select(b => (BookingStatus?)b.Status).SingleOrDefaultAsync(ct);
        if (status is null)
            return NotFound();

        if (status == BookingStatus.AwaitingPayment)
            return NoContent();

        return Problem(statusCode: StatusCodes.Status409Conflict, title: "Эту заявку уже нельзя принять.");
    }
}

public sealed record MasterBookingResponse(Guid Id, string ServiceName, string ClientFirstName, string? ClientLastName,
    BookingStatus Status, DateTime StartAt, DateTime EndAt, decimal Price, string Currency,
    DateTime ResponseDeadlineAt, string? Address, string? Comment, string? CancellationReason);
