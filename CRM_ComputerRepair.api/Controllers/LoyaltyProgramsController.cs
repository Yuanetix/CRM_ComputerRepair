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
[Route("loyalty-programs")]
[Authorize(Roles = "Manager,Admin,Super Admin")]
public class LoyaltyProgramsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;
    private readonly ITenantDbContextFactory _factory;

    public LoyaltyProgramsController(MasterCrmDbContext db, ITenantDbContextFactory factory)
    {
        _db = db;
        _factory = factory;
    }

    // ── Programs ──
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
    {
        var query = _db.LoyaltyPrograms.AsNoTracking();
        if (activeOnly == true)
            query = query.Where(x => x.IsActive);

        var list = await query
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.LoyaltyPrograms.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LoyaltyProgramId == id);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoyaltyProgramRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var program = new LoyaltyProgram
        {
            ProgramName = request.ProgramName.Trim(),
            Description = request.Description?.Trim() ?? "",
            PointsPerPeso = request.PointsPerPeso,
            DiscountPercentage = request.DiscountPercentage,
            MinimumSpend = request.MinimumSpend,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            PointsValidityDays = request.PointsValidityDays,
            RedeemPointsRequired = request.RedeemPointsRequired,
            MinTransactions = request.MinTransactions,
            MinTotalSpent = request.MinTotalSpent,
            MaxInactiveDays = request.MaxInactiveDays,
            MinVisitsPerPeriod = request.MinVisitsPerPeriod,
            VisitPeriodDays = request.VisitPeriodDays,
            RewardType = request.RewardType,
            RewardValue = request.RewardValue,
            MaxRedemptionsPerCustomer = request.MaxRedemptionsPerCustomer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.LoyaltyPrograms.Add(program);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { id = program.LoyaltyProgramId }, program);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,
        [FromBody] UpdateLoyaltyProgramRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var item = await _db.LoyaltyPrograms
            .FirstOrDefaultAsync(x => x.LoyaltyProgramId == id);

        if (item is null) return NotFound();

        item.ProgramName = request.ProgramName.Trim();
        item.Description = request.Description?.Trim() ?? "";
        item.PointsPerPeso = request.PointsPerPeso;
        item.DiscountPercentage = request.DiscountPercentage;
        item.MinimumSpend = request.MinimumSpend;
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.PointsValidityDays = request.PointsValidityDays;
        item.RedeemPointsRequired = request.RedeemPointsRequired;
        item.MinTransactions = request.MinTransactions;
        item.MinTotalSpent = request.MinTotalSpent;
        item.MaxInactiveDays = request.MaxInactiveDays;
        item.MinVisitsPerPeriod = request.MinVisitsPerPeriod;
        item.VisitPeriodDays = request.VisitPeriodDays;
        item.RewardType = request.RewardType;
        item.RewardValue = request.RewardValue;
        item.MaxRedemptionsPerCustomer = request.MaxRedemptionsPerCustomer;
        item.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _db.LoyaltyPrograms
            .FirstOrDefaultAsync(x => x.LoyaltyProgramId == id);

        if (item is null) return NotFound();

        item.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Loyalty program {id} archived." });
    }

    // ── Members ──
    /// <summary>Members (loyalty accounts) joined with tenant customer details.</summary>
    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> GetMembers(int id, [FromQuery] bool? activeOnly = null)
    {
        var program = await _db.LoyaltyPrograms.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoyaltyProgramId == id);
        if (program is null) return NotFound();

        var companyId = UserSessionHelper.GetCompanyId(HttpContext);
        await using var tenant = await _factory.CreateAsync(companyId);

        var accounts = await _db.CustomerLoyaltyAccounts.AsNoTracking()
            .Where(a => a.LoyaltyProgramId == id)
            .ToListAsync();

        if (activeOnly == true)
            accounts = accounts.Where(a => a.IsActive).ToList();

        var tenantCustomers = await tenant.Customers.AsNoTracking()
            .Where(c => accounts.Select(a => a.CustomerId).Contains(c.CustomerId))
            .ToListAsync();

        var byCustomer = tenantCustomers.ToDictionary(c => c.CustomerId);

        var memberIds = accounts.Select(a => a.CustomerId).ToList();
        var tenantPayments = await tenant.Payments.AsNoTracking()
            .Where(p => p.IsPaid && !p.IsVoid)
            .Join(tenant.RepairRequests.AsNoTracking().Where(r => memberIds.Contains(r.CustomerId)),
                p => p.RepairRequestId, r => r.RepairRequestId,
                (p, r) => new { Amount = p.Amount, CustomerId = r.CustomerId })
            .GroupBy(x => x.CustomerId)
            .Select(g => new { CustomerId = g.Key, Spent = g.Sum(x => x.Amount) })
            .ToListAsync();

        var spent = tenantPayments.ToDictionary(t => t.CustomerId, t => t.Spent);

        var result = accounts.Select(a =>
        {
            byCustomer.TryGetValue(a.CustomerId, out var customer);
            return new LoyaltyMemberDto
            {
                CustomerLoyaltyAccountId = a.CustomerLoyaltyAccountId,
                CustomerId = a.CustomerId,
                CustomerName = customer is null
                    ? $"(customer {a.CustomerId})"
                    : $"{customer.FirstName} {customer.LastName}".Trim(),
                Email = customer?.Email,
                Phone = customer?.Phone,
                Points = a.Points,
                TotalSpent = spent.TryGetValue(a.CustomerId, out var s) ? s : 0m,
                JoinedDate = a.JoinedDate,
                IsActive = a.IsActive
            };
        })
        .OrderByDescending(m => m.TotalSpent)
        .ToList();

        return Ok(result);
    }

    /// <summary>Enroll an existing tenant customer into this program.</summary>
    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> Enroll(
        int id, [FromBody] EnrollCustomerRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var program = await _db.LoyaltyPrograms.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoyaltyProgramId == id);
        if (program is null) return NotFound("Loyalty program not found.");

        var companyId = UserSessionHelper.GetCompanyId(HttpContext);
        await using var tenant = await _factory.CreateAsync(companyId);

        var customer = await tenant.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId);
        if (customer is null)
            return BadRequest(new { message = "Customer not found in this company's tenant database." });

        var exists = await _db.CustomerLoyaltyAccounts
            .AnyAsync(a => a.CustomerId == request.CustomerId &&
                           a.LoyaltyProgramId == id);
        if (exists)
            return Conflict(new { message = "Customer is already enrolled." });

        var account = new CustomerLoyaltyAccount
        {
            CustomerId = request.CustomerId,
            LoyaltyProgramId = id,
            Points = request.InitialPoints,
            TotalSpent = 0,
            JoinedDate = DateTime.UtcNow,
            IsActive = true
        };

        _db.CustomerLoyaltyAccounts.Add(account);
        await _db.SaveChangesAsync();

        return Created(string.Empty, account);
    }

    /// <summary>Add or deduct points on a member account.</summary>
    [HttpPut("members/{accountId:int}/points")]
    public async Task<IActionResult> AdjustPoints(
        int accountId, [FromBody] AdjustPointsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var account = await _db.CustomerLoyaltyAccounts
            .FirstOrDefaultAsync(a => a.CustomerLoyaltyAccountId == accountId);
        if (account is null) return NotFound();

        account.Points = Math.Max(0, account.Points + request.PointsChange);
        await _db.SaveChangesAsync();
        return Ok(account);
    }

    /// <summary>Soft-remove a member from the program.</summary>
    [HttpDelete("members/{accountId:int}")]
    public async Task<IActionResult> RemoveMember(int accountId)
    {
        var account = await _db.CustomerLoyaltyAccounts
            .FirstOrDefaultAsync(a => a.CustomerLoyaltyAccountId == accountId);
        if (account is null) return NotFound();

        account.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Member {accountId} removed from program." });
    }
}