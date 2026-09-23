using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("terms")]
[Authorize(Roles = "Admin,Super Admin")]
public class TermsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public TermsController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.TermsAndConditionsSet
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("active")]
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
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.TermsAndConditionsSet.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TermsId == id);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
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
}