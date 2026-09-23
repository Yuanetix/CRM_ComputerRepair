using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("devices")]
[Authorize(Roles = "Admin,Super Admin")]
public class DevicesController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public DevicesController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rows = await _db.Devices
            .Include(d => d.Company)
            .AsNoTracking()
            .ToListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var device = await _db.Devices
            .Include(d => d.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceId == id);

        return device is null ? NotFound() : Ok(device);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Device device)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { id = device.DeviceId }, device);
    }
}