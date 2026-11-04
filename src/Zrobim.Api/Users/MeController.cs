using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;

namespace Zrobim.Api.Users;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(ZrobimDbContext db) : ControllerBase
{
    /// <summary>The signed-in user.</summary>
    [HttpGet]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var me = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserResponse(
                u.Id, u.Email, u.Phone, u.FirstName, u.LastName, u.AvatarUrl, u.Role,
                db.Masters.Where(m => m.UserId == u.Id).Select(m => (Guid?)m.Id).FirstOrDefault()))
            .SingleOrDefaultAsync(ct);

        return me is null ? NotFound() : me;
    }
}
