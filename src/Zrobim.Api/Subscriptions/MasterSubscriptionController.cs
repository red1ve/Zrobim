using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;
using Zrobim.Api.Masters;
using Zrobim.Api.Payments;

namespace Zrobim.Api.Subscriptions;

[ApiController]
[Authorize(Roles = Roles.Master)]
[Route("api/master/subscription")]
public class MasterSubscriptionController(ZrobimDbContext db, TimeProvider time,
    MasterSubscriptionService subscriptions, IOptions<DemoPaymentOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MasterSubscriptionResponse>> Get(CancellationToken ct)
    {
        var masterId = await FindMasterId(ct);
        return masterId is null ? NotFound() : await subscriptions.GetAsync(masterId.Value, ct);
    }

    [HttpPost("demo-payment")]
    public async Task<ActionResult<MasterSubscriptionResponse>> Pay(DemoPaymentRequest request, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return NotFound();
        if (request.Method is not ("CARD" or "ERIP"))
        {
            ModelState.AddModelError(nameof(request.Method), "Выберите способ оплаты.");
            return ValidationProblem(ModelState);
        }

        var masterId = await FindMasterId(ct);
        if (masterId is null)
            return NotFound();

        // The same lock serializes checkout in multiple tabs. A retry while active
        // returns the current period without creating another payment.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await MasterWriteLock.AcquireAsync(db, masterId.Value, ct);
        var now = time.GetUtcNow().UtcDateTime;
        var current = await db.MasterSubscriptions.AsNoTracking()
            .Where(s => s.MasterId == masterId && s.CancelledAt == null && s.ExpiresAt > now)
            .OrderBy(s => s.StartsAt).FirstOrDefaultAsync(ct);
        if (current is not null)
        {
            if (current.StartsAt > now)
                return Problem(statusCode: 409, title: "Подписка уже оформлена на будущий период.");
            return await subscriptions.GetAsync(masterId.Value, ct);
        }

        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(p => p.Code == SubscriptionPlanCode.Basic && p.IsActive, ct);
        if (plan is null)
            return Problem(statusCode: 409, title: "Тариф временно недоступен.");

        var payment = new SubscriptionPayment
        {
            Id = Guid.NewGuid(), MasterId = masterId.Value, PlanId = plan.Id,
            Amount = plan.PriceMonthly, Currency = "BYN", Status = SubscriptionPaymentStatus.Paid,
            Provider = request.Method == "CARD" ? "demo_card" : "demo_erip",
            PaidAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.SubscriptionPayments.Add(payment);
        // The database trigger requires the PAID payment to exist before granting access.
        await db.SaveChangesAsync(ct);
        db.MasterSubscriptions.Add(new MasterSubscription
        {
            MasterId = masterId.Value, PlanId = plan.Id, SubscriptionPaymentId = payment.Id,
            Price = payment.Amount, StartsAt = now, ExpiresAt = now.AddMonths(1), CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await subscriptions.GetAsync(masterId.Value, ct);
    }

    private Task<Guid?> FindMasterId(CancellationToken ct) => db.Masters
        .Where(m => m.UserId == User.GetUserId()).Select(m => (Guid?)m.Id).SingleOrDefaultAsync(ct);
}

public sealed class MasterSubscriptionService(ZrobimDbContext db, TimeProvider time, IOptions<DemoPaymentOptions> options)
{
    public async Task<MasterSubscriptionResponse> GetAsync(Guid masterId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var plan = await db.SubscriptionPlans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Code == SubscriptionPlanCode.Basic && p.IsActive, ct);
        var current = await db.MasterSubscriptions.AsNoTracking()
            .Where(s => s.MasterId == masterId && s.CancelledAt == null && s.StartsAt <= now && s.ExpiresAt > now)
            .Select(s => new { s.StartsAt, s.ExpiresAt }).SingleOrDefaultAsync(ct);
        return new MasterSubscriptionResponse(plan?.PriceMonthly, "BYN", current is not null,
            current?.StartsAt, current?.ExpiresAt, options.Value.Enabled && plan is not null && current is null);
    }
}

public sealed record MasterSubscriptionResponse(decimal? PriceMonthly, string Currency, bool IsActive,
    DateTime? StartsAt, DateTime? ExpiresAt, bool CanPayDemo);
