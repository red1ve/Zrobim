using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zrobim.Api.Auth;
using Zrobim.Api.Bookings;
using Zrobim.Api.Catalog;
using Zrobim.Api.Data;
using Zrobim.Api.Schedule;

namespace Zrobim.Api.Masters;

[ApiController]
[Authorize(Roles = Roles.Master)]
[Route("api/master")]
public class MasterCabinetController(ZrobimDbContext db, TimeProvider time) : ControllerBase
{
    [HttpGet("cabinet")]
    public async Task<ActionResult<MasterCabinetResponse>> GetCabinet(CancellationToken ct)
    {
        var master = await FindMaster(ct);
        if (master is null)
            return NotFound();

        var services = await db.Services.Where(s => s.MasterId == master.Id)
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Select(s => new MasterServiceResponse(s.Id, s.CategoryId, s.Category.Name,
                s.Name, s.Description, s.Price, s.DurationMinutes, s.LocationType, s.IsActive))
            .ToListAsync(ct);
        var categories = await db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new CatalogCategory(c.Id, c.Name)).ToListAsync(ct);
        var hours = await db.WorkingHours.Where(w => w.MasterId == master.Id).OrderBy(w => w.DayOfWeek)
            .Select(w => new WorkdayInput(w.DayOfWeek, w.StartTime, w.EndTime)).ToListAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var blocks = await db.TimeBlocks.Where(b => b.MasterId == master.Id && b.EndAt > now)
            .OrderBy(b => b.StartAt).ThenBy(b => b.Id)
            .Select(b => new TimeBlockResponse(b.Id, b.StartAt, b.EndAt, b.Reason)).ToListAsync(ct);

        var profile = new MasterProfileResponse(master.City, master.Description, master.Address, master.ShowAddressInCatalog);
        return new MasterCabinetResponse(master.VerificationStatus, categories, services, hours, blocks, profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile(SaveMasterProfileRequest request, CancellationToken ct)
    {
        var city = NullIfBlank(request.City);
        var description = NullIfBlank(request.Description);
        var address = NullIfBlank(request.Address);
        if (request.ShowAddressInCatalog && address is null)
        {
            ModelState.AddModelError(nameof(request.Address), "Укажите адрес приёма, чтобы показывать его клиентам.");
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await db.Masters.Where(m => m.UserId == userId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(m => m.City, city)
                .SetProperty(m => m.Description, description)
                .SetProperty(m => m.Address, address)
                .SetProperty(m => m.ShowAddressInCatalog, request.ShowAddressInCatalog)
                .SetProperty(m => m.UpdatedAt, now), ct);
        return changed == 0 ? NotFound() : NoContent();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();


    [HttpPost("services")]
    public Task<IActionResult> CreateService(SaveServiceRequest request, CancellationToken ct) =>
        SaveService(null, request, ct);

    [HttpPut("services/{id:guid}")]
    public Task<IActionResult> EditService(Guid id, SaveServiceRequest request, CancellationToken ct) =>
        SaveService(id, request, ct);

    private async Task<IActionResult> SaveService(Guid? id, SaveServiceRequest request, CancellationToken ct)
    {
        if (request.Price < 0.01m || request.Price > 9_999_999_999.99m ||
            decimal.Round(request.Price, 2) != request.Price)
            ModelState.AddModelError(nameof(request.Price), "Укажите положительную цену с точностью до копеек.");
        if (!Enum.IsDefined(request.LocationType!.Value))
            ModelState.AddModelError(nameof(request.LocationType), "Выберите место оказания услуги.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var master = await FindMaster(ct);
        if (master is null)
            return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await MasterWriteLock.AcquireAsync(db, master.Id, ct);
        var service = id is null
            ? new Service { MasterId = master.Id }
            : await db.Services.SingleOrDefaultAsync(s => s.Id == id && s.MasterId == master.Id, ct);
        if (service is null)
            return NotFound();

        var categoryId = request.CategoryId!.Value;
        if ((id is null || service.CategoryId != categoryId) &&
            !await db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
        {
            ModelState.AddModelError(nameof(request.CategoryId), "Выберите доступную категорию.");
            return ValidationProblem(ModelState);
        }

        service.CategoryId = categoryId;
        service.Name = request.Name.Trim();
        service.Description = NullIfBlank(request.Description);
        service.Price = request.Price;
        service.DurationMinutes = request.DurationMinutes;
        service.LocationType = request.LocationType.Value;
        service.IsActive = request.IsActive!.Value;

        if (id is null)
            db.Services.Add(service);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return id is null ? StatusCode(StatusCodes.Status201Created, new { service.Id }) : NoContent();
    }

    [HttpPut("working-hours")]
    public async Task<IActionResult> SaveWorkingHours(SaveWorkingHoursRequest request, CancellationToken ct)
    {
        var days = request.Days;
        if (days.Count > 7 || days.Any(d => d is null || d.DayOfWeek is < 1 or > 7 ||
            d.StartTime is null || d.EndTime is null || d.EndTime <= d.StartTime) ||
            days.Select(d => d.DayOfWeek).Distinct().Count() != days.Count)
        {
            ModelState.AddModelError(nameof(request.Days), "Укажите один рабочий интервал на день. Конец должен быть позже начала.");
            return ValidationProblem(ModelState);
        }

        var master = await FindMaster(ct);
        if (master is null)
            return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await MasterWriteLock.AcquireAsync(db, master.Id, ct);
        var current = await db.WorkingHours.Where(w => w.MasterId == master.Id).ToDictionaryAsync(w => w.DayOfWeek, ct);
        foreach (var day in days)
        {
            if (!current.Remove(day.DayOfWeek, out var hours))
                db.WorkingHours.Add(hours = new WorkingHours { MasterId = master.Id, DayOfWeek = day.DayOfWeek });
            hours.StartTime = day.StartTime!.Value;
            hours.EndTime = day.EndTime!.Value;
        }
        db.WorkingHours.RemoveRange(current.Values);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("time-blocks")]
    public async Task<IActionResult> AddTimeBlock(CreateTimeBlockRequest request, CancellationToken ct)
    {
        var localStart = request.LocalStart!.Value;
        var localEnd = request.LocalEnd!.Value;
        if (localStart.Kind != DateTimeKind.Unspecified || localEnd.Kind != DateTimeKind.Unspecified || localEnd <= localStart)
        {
            ModelState.AddModelError(nameof(request.LocalEnd), "Укажите начало и конец по Минску. Конец должен быть позже начала.");
            return ValidationProblem(ModelState);
        }

        var start = AvailabilityService.ToUtc(localStart);
        var end = AvailabilityService.ToUtc(localEnd);
        var master = await FindMaster(ct);
        if (master is null)
            return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await MasterWriteLock.AcquireAsync(db, master.Id, ct);
        if (start < time.GetUtcNow().UtcDateTime)
        {
            ModelState.AddModelError(nameof(request.LocalStart), "Выберите начало в будущем.");
            return ValidationProblem(ModelState);
        }

        if (await db.Bookings.HoldingTime().AnyAsync(b => b.MasterId == master.Id &&
                b.StartAt < end && start < b.EndAt, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "На это время уже есть активная запись.");

        if (await db.TimeBlocks.AnyAsync(b => b.MasterId == master.Id && b.StartAt < end && start < b.EndAt, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Это время уже перекрыто блокировкой.");

        var block = new TimeBlock
        {
            MasterId = master.Id, StartAt = start, EndAt = end,
            Reason = NullIfBlank(request.Reason)
        };
        db.TimeBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new TimeBlockResponse(block.Id, start, end, block.Reason));
    }

    [HttpDelete("time-blocks/{id:guid}")]
    public async Task<IActionResult> RemoveTimeBlock(Guid id, CancellationToken ct)
    {
        var master = await FindMaster(ct);
        if (master is null)
            return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await MasterWriteLock.AcquireAsync(db, master.Id, ct);
        var removed = await db.TimeBlocks.Where(b => b.MasterId == master.Id && b.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return removed == 0 ? NotFound() : NoContent();
    }

    private Task<Master?> FindMaster(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return db.Masters.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId, ct);
    }
}

public sealed record SaveServiceRequest([Required] Guid? CategoryId, [Required, MaxLength(150)] string Name,
    [MaxLength(2000)] string? Description, decimal Price, [Range(1, 1440)] int DurationMinutes,
    [Required] LocationType? LocationType, [Required] bool? IsActive);

public sealed record SaveWorkingHoursRequest([Required] List<WorkdayInput> Days);

public sealed record WorkdayInput(int DayOfWeek, TimeOnly? StartTime, TimeOnly? EndTime);

public sealed record CreateTimeBlockRequest([Required] DateTime? LocalStart, [Required] DateTime? LocalEnd,
    [MaxLength(255)] string? Reason);

public sealed record MasterServiceResponse(Guid Id, Guid CategoryId, string CategoryName, string Name,
    string? Description, decimal Price, int DurationMinutes, LocationType LocationType, bool IsActive);

public sealed record TimeBlockResponse(Guid Id, DateTime StartAt, DateTime EndAt, string? Reason);

public sealed record MasterCabinetResponse(VerificationStatus VerificationStatus, List<CatalogCategory> Categories,
    List<MasterServiceResponse> Services, List<WorkdayInput> WorkingHours, List<TimeBlockResponse> TimeBlocks,
    MasterProfileResponse Profile);

public sealed record SaveMasterProfileRequest(
    [MaxLength(100, ErrorMessage = "Город должен быть не длиннее 100 символов.")] string? City,
    [MaxLength(2000, ErrorMessage = "Описание должно быть не длиннее 2000 символов.")] string? Description,
    [MaxLength(500, ErrorMessage = "Адрес должен быть не длиннее 500 символов.")] string? Address,
    bool ShowAddressInCatalog);
