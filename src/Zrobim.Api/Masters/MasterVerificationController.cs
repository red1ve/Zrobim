using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;
using Zrobim.Api.Users;

namespace Zrobim.Api.Masters;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/masters")]
public class MasterVerificationController(ZrobimDbContext db, TimeProvider time) : ControllerBase
{
    private IQueryable<Master> ActiveMasters =>
        db.Masters.Where(m => m.User.IsActive && m.User.Role == UserRole.Master);

    [HttpGet("pending")]
    public Task<List<PendingMasterResponse>> GetPending(CancellationToken ct) =>
        ActiveMasters.Where(m => m.VerificationStatus == VerificationStatus.Pending)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new PendingMasterResponse(m.Id, m.User.FirstName, m.User.LastName, m.User.Email,
                db.Services.Count(s => s.MasterId == m.Id && s.IsActive && s.Category.IsActive),
                db.WorkingHours.Count(w => w.MasterId == m.Id),
                new MasterProfileResponse(m.City, m.Description, m.Address, m.ShowAddressInCatalog)))
            .ToListAsync(ct);

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var master = ActiveMasters.Where(m => m.Id == id);
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await master.Where(m => m.VerificationStatus == VerificationStatus.Pending)
            .ExecuteUpdateAsync(set => set
                .SetProperty(m => m.VerificationStatus, VerificationStatus.Approved)
                .SetProperty(m => m.UpdatedAt, now), ct);
        if (changed == 1)
            return NoContent();

        var status = await master.Select(m => (VerificationStatus?)m.VerificationStatus).SingleOrDefaultAsync(ct);
        return status switch
        {
            null => NotFound(),
            VerificationStatus.Approved => NoContent(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Этот профиль уже не ожидает одобрения.")
        };
    }
}

public sealed record PendingMasterResponse(Guid Id, string FirstName, string? LastName, string Email,
    int ActiveServices, int WorkingDays, MasterProfileResponse Profile);
