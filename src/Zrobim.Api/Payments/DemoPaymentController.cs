using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Zrobim.Api.Auth;
using Zrobim.Api.Bookings;
using Zrobim.Api.Data;

namespace Zrobim.Api.Payments;

[ApiController]
[Authorize(Roles = Roles.Client)]
[Route("api/bookings/{bookingId:guid}/demo-payment")]
public class DemoPaymentController(ZrobimDbContext db, TimeProvider time, IOptions<DemoPaymentOptions> options)
    : ControllerBase
{
    private const string CardProvider = "demo_card";
    private const string EripProvider = "demo_erip";

    [HttpPost]
    public async Task<ActionResult<DemoPaymentReceipt>> Pay(Guid bookingId, DemoPaymentRequest request, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return NotFound();

        if (request.Method is not ("CARD" or "ERIP"))
        {
            ModelState.AddModelError(nameof(request.Method), "Выберите способ оплаты.");
            return ValidationProblem(ModelState);
        }

        var clientId = User.GetUserId();
        var owned = db.Bookings.Where(b => b.Id == bookingId && b.ClientId == clientId);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await owned.OpenForClientCancellation(now)
            .Where(b => b.Status == BookingStatus.AwaitingPayment)
            .WithoutActivePayment(db)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.Status, BookingStatus.Confirmed)
                .SetProperty(b => b.UpdatedAt, now), ct);

        if (changed == 0)
        {
            var previous = await db.Payments
                .Where(p => p.BookingId == bookingId && p.Booking.ClientId == clientId &&
                    p.Booking.Status == BookingStatus.Confirmed && p.Status == PaymentStatus.Held &&
                    (p.Provider == CardProvider || p.Provider == EripProvider))
                .Select(p => new DemoPaymentReceipt(p.BookingId, p.Amount, p.Currency,
                    p.Provider == CardProvider ? "CARD" : "ERIP"))
                .SingleOrDefaultAsync(ct);
            if (previous is not null)
                return previous;
            return await owned.AnyAsync(ct) ? PaymentUnavailable() : NotFound();
        }

        var amount = await owned.Select(b => b.Price).SingleAsync(ct);
        var payment = new Payment
        {
            BookingId = bookingId,
            Amount = amount,
            Currency = "BYN",
            Status = PaymentStatus.Held,
            Provider = request.Method == "CARD" ? CardProvider : EripProvider,
            PaidAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(20)
        };
        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "payments_one_alive_per_booking"
            })
        {
            return PaymentUnavailable();
        }

        await transaction.CommitAsync(ct);
        return new DemoPaymentReceipt(bookingId, amount, payment.Currency, request.Method);
    }

    private ObjectResult PaymentUnavailable() =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Эту запись уже нельзя оплатить. Обновите список записей.");
}

public sealed class DemoPaymentOptions
{
    public bool Enabled { get; set; }
}

public sealed record DemoPaymentRequest([Required] string Method);

public sealed record DemoPaymentReceipt(Guid BookingId, decimal Amount, string Currency, string Method);
