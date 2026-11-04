using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;

namespace Zrobim.Api.Notifications;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(ZrobimDbContext db) : ControllerBase
{
    private IQueryable<Notification> OwnNotifications(Guid userId) =>
        db.Notifications.Where(n => n.UserId == userId);

    [HttpGet]
    public async Task<NotificationPage> GetPage(CancellationToken ct,
        [FromQuery, Range(1, 1000)] int page = 1)
    {
        const int pageSize = 20;
        var items = await OwnNotifications(User.GetUserId())
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.Title, n.Message, n.BookingId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
        return new NotificationPage(items.Take(pageSize).ToArray(), page < 1000 && items.Count > pageSize);
    }

    [HttpGet("unread-count")]
    public async Task<UnreadNotificationCount> GetUnreadCount(CancellationToken ct) =>
        new(await OwnNotifications(User.GetUserId()).CountAsync(n => !n.IsRead, ct));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var own = OwnNotifications(User.GetUserId()).Where(n => n.Id == id);
        var changed = await own.Where(n => !n.IsRead)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true), ct);
        return changed == 1 || await own.AnyAsync(ct) ? NoContent() : NotFound();
    }
}

public sealed record NotificationPage(IReadOnlyList<NotificationResponse> Items, bool HasMore);
public sealed record NotificationResponse(Guid Id, string Type, string Title, string? Message,
    Guid? BookingId, bool IsRead, DateTime CreatedAt);
public sealed record UnreadNotificationCount(int Count);
