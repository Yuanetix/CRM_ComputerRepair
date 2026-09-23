using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/payments")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class PaymentsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly IAuditWriter _audit;

    public PaymentsController(ITenantDbContextFactory factory, IAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        [FromQuery] bool? paidOnly = null,
        [FromQuery] bool? includeVoid = false)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.Payments.AsNoTracking();

        if (from.HasValue) query = query.Where(p => p.PaymentDate >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(p => p.PaymentDate <= to.Value.ToUniversalTime());
        if (paidOnly == true) query = query.Where(p => p.IsPaid);
        if (includeVoid != true) query = query.Where(p => !p.IsVoid);

        if (customerId.HasValue)
        {
            var cid = customerId.Value;
            var repairIds = await db.RepairRequests.AsNoTracking()
                .Where(r => r.CustomerId == cid)
                .Select(r => r.RepairRequestId)
                .ToListAsync();

            query = query.Where(p => repairIds.Contains(p.RepairRequestId));
        }

        var list = await query
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        // Attach request/customer info for the transactions grid.
        var result = new List<Dictionary<string, object?>>();
        var repairs = await db.RepairRequests.AsNoTracking()
            .Where(r => list.Select(p => p.RepairRequestId).Contains(r.RepairRequestId))
            .ToListAsync();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => repairs.Select(r => r.CustomerId).Contains(c.CustomerId))
            .ToListAsync();

        var customerById = customers.ToDictionary(c => c.CustomerId, c => $"{c.FirstName} {c.LastName}".Trim());

        foreach (var p in list)
        {
            var r = repairs.FirstOrDefault(x => x.RepairRequestId == p.RepairRequestId);
            result.Add(new Dictionary<string, object?>
            {
                ["paymentId"] = p.PaymentId,
                ["repairRequestId"] = p.RepairRequestId,
                ["requestNumber"] = r?.RequestNumber ?? "-",
                ["customerId"] = r?.CustomerId,
                ["customer"] = r != null && customerById.TryGetValue(r.CustomerId, out var n) ? n : "-",
                ["service"] = r?.DeviceModel ?? "-",
                ["paymentDate"] = p.PaymentDate,
                ["amount"] = p.Amount,
                ["paymentMethod"] = p.PaymentMethod ?? "-",
                ["referenceNumber"] = p.ReferenceNumber ?? "",
                ["isPaid"] = p.IsPaid,
                ["isVoid"] = p.IsVoid
            });
        }

        return Ok(result);
    }

    [HttpGet("{paymentId:int}")]
    public async Task<IActionResult> GetById(int companyId, int paymentId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreatePaymentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var repair = await db.RepairRequests
            .FirstOrDefaultAsync(r => r.RepairRequestId == request.RepairRequestId);

        if (repair is null)
            return BadRequest(new { message = "Repair request not found." });

        var payment = new Payment
        {
            RepairRequestId = request.RepairRequestId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod?.Trim(),
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            IsPaid = request.IsPaid,
            IsVoid = false
        };

        db.Payments.Add(payment);

        // A paid transaction earns loyalty points for the customer.
        if (request.IsPaid && repair.CustomerId > 0)
            await GrantLoyaltyPoints(db, companyId, repair.CustomerId, request.Amount);

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Create", "Payment",
            payment.PaymentId.ToString(),
            $"Payment \u20b1{payment.Amount:N2} for {repair.RequestNumber}");

        return CreatedAtAction(nameof(GetById),
            new { companyId, paymentId = payment.PaymentId }, payment);
    }

    [HttpPut("{paymentId:int}")]
    public async Task<IActionResult> Update(
        int companyId, int paymentId, [FromBody] UpdatePaymentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.Payments
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (item is null) return NotFound();

        item.Amount = request.Amount;
        item.PaymentDate = request.PaymentDate ?? item.PaymentDate;
        item.PaymentMethod = request.PaymentMethod?.Trim() ?? item.PaymentMethod;
        item.ReferenceNumber = request.ReferenceNumber?.Trim() ?? item.ReferenceNumber;
        item.IsPaid = request.IsPaid;

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Update", "Payment",
            item.PaymentId.ToString(),
            $"Payment {item.PaymentId} updated \u20b1{item.Amount:N2}");

        return Ok(item);
    }

    /// <summary>Soft-void a payment (keeps audit history, removes from sales totals).</summary>
    [HttpDelete("{paymentId:int}")]
    public async Task<IActionResult> Void(int companyId, int paymentId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.Payments
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (item is null) return NotFound();

        item.IsVoid = true;
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Void", "Payment",
            item.PaymentId.ToString(),
            $"Payment {item.PaymentId} \u20b1{item.Amount:N2} voided");

        return Ok(new { message = $"Payment {paymentId} voided." });
    }

    private static async Task GrantLoyaltyPoints(
        TenantCrmDbContext db, int companyId, int customerId, decimal amount)
    {
        // Loyalty accounts live in the master database; the tenant context can not
        // touch them, so the points awarded against the customer balance are tracked
        // here. The master-side account balance is updated by retention/analytics
        // consumers via the master context when needed.
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null) return;

        var points = (int)Math.Floor(amount); // 1 point per peso
        customer.LoyaltyPoints = (customer.LoyaltyPoints ?? 0) + points;
    }
}