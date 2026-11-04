using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Zrobim.Api.Auth;
using Zrobim.Api.Catalog;
using Zrobim.Api.Data;
using Zrobim.Api.Masters;
using Zrobim.Api.Payments;
using Zrobim.Api.Schedule;

namespace Zrobim.Api.Bookings;

[ApiController]
[Authorize(Roles = Roles.Client)]
[Route("api/bookings")]
public class BookingsController(ZrobimDbContext db, AvailabilityService availability, TimeProvider time,
    IOptions<DemoPaymentOptions> demoPayment)
    : ControllerBase
{
    [HttpGet]
    public Task<List<BookingResponse>> GetMine(CancellationToken ct)
    {
        var clientId = User.GetUserId();
        var now = time.GetUtcNow().UtcDateTime;
        var demoPaymentEnabled = demoPayment.Value.Enabled;
        var ownBookings = db.Bookings.Where(b => b.ClientId == clientId);
        var cancellable = ownBookings.OpenForClientCancellation(now).WithoutActivePayment(db);

        return ownBookings
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Select(b => new BookingResponse(b.Id, b.Service.Name,
                b.Master.User.FirstName, b.Master.User.LastName, b.Status,
                b.StartAt, b.EndAt, b.Price, "BYN", b.ResponseDeadlineAt, b.CancellationReason,
                cancellable.Any(candidate => candidate.Id == b.Id),
                demoPaymentEnabled && b.Status == BookingStatus.AwaitingPayment &&
                cancellable.Any(candidate => candidate.Id == b.Id)))
            .ToListAsync(ct);
    }

    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var masterId = await db.Services.VisibleInCatalog().Where(s => s.Id == request.ServiceId)
            .Select(s => (Guid?)s.MasterId).SingleOrDefaultAsync(ct);
        if (masterId is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Услуга больше недоступна.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await MasterWriteLock.AcquireAsync(db, masterId.Value, ct);


        var service = await db.Services.VisibleInCatalog()
            .Where(s => s.Id == request.ServiceId)
            .Select(s => new
            {
                s.Id, s.MasterId, s.Name, s.DurationMinutes, s.Price, s.LocationType,
                s.Master.User.FirstName, s.Master.User.LastName
            })
            .SingleOrDefaultAsync(ct);

        if (service is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Услуга больше недоступна.");

        var address = service.LocationType == LocationType.ClientPlace ? request.Address?.Trim() : null;
        if (service.LocationType == LocationType.ClientPlace && string.IsNullOrWhiteSpace(address))
        {
            ModelState.AddModelError(nameof(request.Address), "Укажите адрес оказания услуги.");
            return ValidationProblem(ModelState);
        }

        var start = request.StartAt!.Value.UtcDateTime;
        var available = await availability.GetAsync(service.MasterId, service.DurationMinutes, ct);
        var slot = available.Slots.SingleOrDefault(s => s.StartAt == start);
        if (slot is null)
            return SlotUnavailable();

        var now = time.GetUtcNow().UtcDateTime;
        var responseDeadline = AvailabilityService.GetResponseDeadline(now, start);
        if (responseDeadline <= now)
            return SlotUnavailable();

        var booking = new Booking
        {
            ClientId = User.GetUserId(),
            MasterId = service.MasterId,
            ServiceId = service.Id,
            StartAt = slot.StartAt,
            EndAt = slot.EndAt,
            Price = service.Price,
            DepositAmount = Math.Max(0.01m, Math.Round(service.Price * 0.30m, 2, MidpointRounding.AwayFromZero)),
            LocationType = service.LocationType,
            Address = address,
            Status = BookingStatus.Pending,
            ClientComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ResponseDeadlineAt = responseDeadline
        };

        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ExclusionViolation,
                ConstraintName: "bookings_no_overlap"
            })
        {
            return SlotUnavailable();
        }

        await transaction.CommitAsync(ct);
        return StatusCode(StatusCodes.Status201Created,
            new BookingResponse(booking.Id, service.Name, service.FirstName, service.LastName,
                booking.Status, booking.StartAt, booking.EndAt, booking.Price, "BYN", booking.ResponseDeadlineAt,
                booking.CancellationReason, CanCancel: true, CanPayDemo: false));
    }

    private ObjectResult SlotUnavailable() =>
        Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Это время уже недоступно. Выберите другой интервал.");
}

public sealed record CreateBookingRequest(
    [Required] Guid? ServiceId,
    [Required] DateTimeOffset? StartAt,
    [MaxLength(500, ErrorMessage = "Адрес должен быть не длиннее 500 символов.")] string? Address,
    [MaxLength(1000, ErrorMessage = "Комментарий должен быть не длиннее 1000 символов.")] string? Comment);

public sealed record BookingResponse(Guid Id, string ServiceName, string MasterFirstName, string? MasterLastName,
    BookingStatus Status, DateTime StartAt, DateTime EndAt, decimal Price, string Currency, DateTime ResponseDeadlineAt,
    string? CancellationReason, bool CanCancel, bool CanPayDemo);
