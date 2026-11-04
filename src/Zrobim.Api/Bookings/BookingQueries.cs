namespace Zrobim.Api.Bookings;

internal static class BookingQueries
{
    public static IQueryable<Booking> HoldingTime(this IQueryable<Booking> bookings) =>
        bookings.Where(b => b.Status == BookingStatus.Pending ||
            b.Status == BookingStatus.AwaitingPayment || b.Status == BookingStatus.Confirmed);
}
