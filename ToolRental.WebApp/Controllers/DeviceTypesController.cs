using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;
using ToolRental.WebApp.DTOs;

namespace ToolRental.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceTypesController : ControllerBase
{
    private readonly ToolRentalDbContext _context;

    public DeviceTypesController(ToolRentalDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceTypeDto>>> GetAll()
    {
        var types = await _context.DeviceTypes
            .Include(dt => dt.Devices)
            .OrderBy(dt => dt.TypeName)
            .Select(dt => new DeviceTypeDto
            {
                Id = dt.Id,
                TypeName = dt.TypeName,
                DeviceCount = dt.Devices.Count
            })
            .ToListAsync();

        return Ok(types);
    }

    [HttpPost]
    public async Task<ActionResult<DeviceTypeDto>> Create([FromBody] DeviceTypeCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TypeName))
            return BadRequest("A típus neve kötelező.");

        if (dto.TypeName.Length > 100)
            return BadRequest("A típus neve maximum 100 karakter lehet.");

        var deviceType = new ToolRental.Core.Models.DeviceType
        {
            TypeName = dto.TypeName.Trim()
        };

        _context.DeviceTypes.Add(deviceType);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new DeviceTypeDto
        {
            Id = deviceType.Id,
            TypeName = deviceType.TypeName,
            DeviceCount = 0
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DeviceTypeDto>> Update(int id, [FromBody] DeviceTypeCreateUpdateDto dto)
    {
        var deviceType = await _context.DeviceTypes.FindAsync(id);
        if (deviceType == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(dto.TypeName))
            return BadRequest("A típus neve kötelező.");

        if (dto.TypeName.Length > 100)
            return BadRequest("A típus neve maximum 100 karakter lehet.");

        deviceType.TypeName = dto.TypeName.Trim();
        await _context.SaveChangesAsync();

        var count = await _context.Devices.CountAsync(d => d.DeviceType == id);

        return Ok(new DeviceTypeDto
        {
            Id = deviceType.Id,
            TypeName = deviceType.TypeName,
            DeviceCount = count
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deviceType = await _context.DeviceTypes.FindAsync(id);
        if (deviceType == null)
            return NotFound();

        var hasDevices = await _context.Devices.AnyAsync(d => d.DeviceType == id);
        if (hasDevices)
            return Conflict("Nem törölhető, mert vannak hozzá tartozó eszközök.");

        _context.DeviceTypes.Remove(deviceType);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
