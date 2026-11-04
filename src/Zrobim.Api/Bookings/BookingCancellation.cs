using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Data;
using Zrobim.Api.Payments;

namespace Zrobim.Api.Bookings;

internal static class BookingCancellation
{
    public static IQueryable<Booking> WithoutActivePayment(this IQueryable<Booking> bookings, ZrobimDbContext db) =>
        bookings.Where(b => !db.Payments.Any(p => p.BookingId == b.Id && p.Status != PaymentStatus.Failed));

    public static IQueryable<Booking> OpenForClientCancellation(this IQueryable<Booking> bookings, DateTime now) =>
        bookings.Where(b => b.StartAt > now &&
            ((b.Status == BookingStatus.Pending && b.ResponseDeadlineAt > now) ||
             (b.Status == BookingStatus.AwaitingPayment && b.PaymentDeadlineAt > now)));

    public static Task<int> CancelAsync(this IQueryable<Booking> eligible, DateTime now, Guid? actorId,
        Expression<Func<Booking, string?>> reason, CancellationToken ct) =>
        eligible.ExecuteUpdateAsync(set => set
            .SetProperty(b => b.Status, BookingStatus.Cancelled)
            .SetProperty(b => b.CancelledAt, now)
            .SetProperty(b => b.UpdatedAt, now)
            .SetProperty(b => b.CancelledBy, actorId)
            .SetProperty(b => b.CancellationReason, reason), ct);
}
