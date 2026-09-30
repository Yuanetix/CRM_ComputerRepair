using System.Security.Claims;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("terms")]
public class TermsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;
    private readonly IAuditWriter _audit;

    public TermsController(MasterCrmDbContext db, IAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Roles = "Super Admin,Admin")]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.TermsAndConditionsSet
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var item = await _db.TermsAndConditionsSet
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Super Admin,Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.TermsAndConditionsSet.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TermsId == id);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> Create([FromBody] CreateTermsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Deactivate existing active terms (only one active at a time)
        var existing = await _db.TermsAndConditionsSet
            .Where(x => x.IsActive)
            .ToListAsync();
        foreach (var old in existing)
            old.IsActive = false;

        var terms = new TermsAndConditions
        {
            Title = request.Title.Trim(),
            Content = request.Content,
            Version = DateTime.UtcNow,
            IsActive = true,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.TermsAndConditionsSet.Add(terms);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { id = terms.TermsId }, terms);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> Update(int id,
        [FromBody] UpdateTermsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var item = await _db.TermsAndConditionsSet
            .FirstOrDefaultAsync(x => x.TermsId == id);

        if (item is null) return NotFound();

        item.Title = request.Title.Trim();
        item.Content = request.Content;
        item.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        var item = await _db.TermsAndConditionsSet
            .FirstOrDefaultAsync(x => x.TermsId == id);

        if (item is null) return NotFound();

        // Deactivate all others
        var others = await _db.TermsAndConditionsSet
            .Where(x => x.TermsId != id && x.IsActive)
            .ToListAsync();
        foreach (var other in others)
            other.IsActive = false;

        item.IsActive = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Terms {id} activated.", item.TermsId, item.IsActive });
    }

    /// <summary>
    /// Called when a tenant logs in and accepts the active Terms and Conditions.
    /// </summary>
    [HttpPost("accept")]
    [Authorize]
    public async Task<IActionResult> AcceptTerms()
    {
        int companyId = UserSessionHelper.GetCompanyId(HttpContext);
        if (companyId <= 0)
            return BadRequest(new { error = "Valid Company ID is required to accept terms." });

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
        if (company is null)
            return NotFound(new { error = $"Company ID {companyId} not found." });

        var activeTerms = await _db.TermsAndConditionsSet.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        company.HasAcceptedTerms = true;
        company.TermsAcceptedAt = DateTime.UtcNow;
        company.TermsAcceptedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
        company.AcceptedTermsId = activeTerms?.TermsId;
        company.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name) ?? "User";
        await _audit.WriteAsync(
            username,
            "AcceptTerms",
            "Company",
            company.CompanyId.ToString(),
            $"Company '{company.CompanyName}' ({company.CompanyCode}) accepted Terms version '{activeTerms?.Title ?? "Standard Agreement"}' (Terms ID: {activeTerms?.TermsId}).");

        return Ok(new
        {
            success = true,
            message = "Terms & Conditions successfully accepted.",
            companyId = company.CompanyId,
            hasAcceptedTerms = company.HasAcceptedTerms,
            termsAcceptedAt = company.TermsAcceptedAt,
            acceptedTermsId = company.AcceptedTermsId
        });
    }

    /// <summary>
    /// Records that the tenant declined the terms and conditions.
    /// </summary>
    [HttpPost("reject")]
    [Authorize]
    public async Task<IActionResult> RejectTerms()
    {
        int companyId = UserSessionHelper.GetCompanyId(HttpContext);
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
        if (company is not null)
        {
            var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name) ?? "User";
            await _audit.WriteAsync(
                username,
                "RejectTerms",
                "Company",
                company.CompanyId.ToString(),
                $"Company '{company.CompanyName}' ({company.CompanyCode}) declined the Platform Terms & Conditions upon onboarding/login.");
        }

        return Ok(new { success = true, message = "Terms decline recorded." });
    }

    /// <summary>
    /// Returns the active terms acceptance status for the current company.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        int companyId = UserSessionHelper.GetCompanyId(HttpContext);
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
        if (company is null)
            return NotFound(new { error = $"Company ID {companyId} not found." });

        var activeTerms = await _db.TermsAndConditionsSet.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            companyId = company.CompanyId,
            companyName = company.CompanyName,
            companyCode = company.CompanyCode,
            hasAcceptedTerms = company.HasAcceptedTerms,
            termsAcceptedAt = company.TermsAcceptedAt,
            termsAcceptedByUserId = company.TermsAcceptedByUserId,
            acceptedTermsId = company.AcceptedTermsId,
            activeTermsId = activeTerms?.TermsId,
            activeTermsTitle = activeTerms?.Title
        });
    }
}