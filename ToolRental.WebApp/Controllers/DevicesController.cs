using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolRental.Core.Models;
using ToolRental.Data;
using ToolRental.WebApp.DTOs;

namespace ToolRental.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly ToolRentalDbContext _context;
    private readonly IWebHostEnvironment _env;

    public DevicesController(ToolRentalDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? typeId,
        [FromQuery] bool? available)
    {
        var query = _context.Devices
            .Include(d => d.DeviceTypeNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(d =>
                d.DeviceName.ToLower().Contains(s) ||
                (d.Serial != null && d.Serial.ToLower().Contains(s)) ||
                (d.DeviceTypeNavigation != null && d.DeviceTypeNavigation.TypeName.ToLower().Contains(s)));
        }

        if (typeId.HasValue)
            query = query.Where(d => d.DeviceType == typeId.Value);

        if (available.HasValue)
            query = query.Where(d => d.Available == available.Value);

        var devices = await query
            .OrderBy(d => d.DeviceName)
            .Select(d => new DeviceDto
            {
                Id = d.Id,
                DeviceName = d.DeviceName,
                DeviceType = d.DeviceType,
                DeviceTypeName = d.DeviceTypeNavigation != null ? d.DeviceTypeNavigation.TypeName : "",
                Serial = d.Serial,
                Price = d.Price,
                RentPrice = d.RentPrice,
                Available = d.Available,
                Picture = d.Picture,
                RentCount = d.RentCount,
                Notes = d.Notes,
                ReservedUntil = d.ReservedUntil
            })
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> GetById(int id)
    {
        var d = await _context.Devices
            .Include(d => d.DeviceTypeNavigation)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (d == null)
            return NotFound();

        return Ok(new DeviceDto
        {
            Id = d.Id,
            DeviceName = d.DeviceName,
            DeviceType = d.DeviceType,
            DeviceTypeName = d.DeviceTypeNavigation?.TypeName ?? "",
            Serial = d.Serial,
            Price = d.Price,
            RentPrice = d.RentPrice,
            Available = d.Available,
            Picture = d.Picture,
            RentCount = d.RentCount,
            Notes = d.Notes,
            ReservedUntil = d.ReservedUntil
        });
    }

    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Create(
        [FromForm] DeviceCreateUpdateDto dto,
        IFormFile? image)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceName))
            return BadRequest("Az eszköz neve kötelező.");

        var typeExists = await _context.DeviceTypes.AnyAsync(dt => dt.Id == dto.DeviceType);
        if (!typeExists)
            return BadRequest("A megadott eszköztípus nem létezik.");

        var device = new Device
        {
            DeviceName = dto.DeviceName.Trim(),
            DeviceType = dto.DeviceType,
            Serial = dto.Serial?.Trim() ?? string.Empty,
            Price = dto.Price,
            RentPrice = dto.RentPrice,
            Available = dto.Available,
            Notes = dto.Notes?.Trim()
        };

        if (image != null)
        {
            var picturePath = await SaveImage(image);
            if (picturePath == null)
                return BadRequest("Nem támogatott képformátum. Engedélyezett: jpg, png, bmp, gif, webp.");
            device.Picture = picturePath;
        }

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        await _context.Entry(device).Reference(d => d.DeviceTypeNavigation).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = device.Id }, new DeviceDto
        {
            Id = device.Id,
            DeviceName = device.DeviceName,
            DeviceType = device.DeviceType,
            DeviceTypeName = device.DeviceTypeNavigation?.TypeName ?? "",
            Serial = device.Serial,
            Price = device.Price,
            RentPrice = device.RentPrice,
            Available = device.Available,
            Picture = device.Picture,
            RentCount = device.RentCount,
            Notes = device.Notes,
            ReservedUntil = device.ReservedUntil
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DeviceDto>> Update(
        int id,
        [FromForm] DeviceCreateUpdateDto dto,
        IFormFile? image)
    {
        var device = await _context.Devices
            .Include(d => d.DeviceTypeNavigation)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(dto.DeviceName))
            return BadRequest("Az eszköz neve kötelező.");

        var typeExists = await _context.DeviceTypes.AnyAsync(dt => dt.Id == dto.DeviceType);
        if (!typeExists)
            return BadRequest("A megadott eszköztípus nem létezik.");

        device.DeviceName = dto.DeviceName.Trim();
        device.DeviceType = dto.DeviceType;
        device.Serial = dto.Serial?.Trim() ?? string.Empty;
        device.Price = dto.Price;
        device.RentPrice = dto.RentPrice;
        device.Available = dto.Available;
        device.Notes = dto.Notes?.Trim();

        if (image != null)
        {
            DeleteImageFile(device.Picture);
            var picturePath = await SaveImage(image);
            if (picturePath == null)
                return BadRequest("Nem támogatott képformátum. Engedélyezett: jpg, png, bmp, gif, webp.");
            device.Picture = picturePath;
        }

        await _context.SaveChangesAsync();

        await _context.Entry(device).Reference(d => d.DeviceTypeNavigation).LoadAsync();

        return Ok(new DeviceDto
        {
            Id = device.Id,
            DeviceName = device.DeviceName,
            DeviceType = device.DeviceType,
            DeviceTypeName = device.DeviceTypeNavigation?.TypeName ?? "",
            Serial = device.Serial,
            Price = device.Price,
            RentPrice = device.RentPrice,
            Available = device.Available,
            Picture = device.Picture,
            RentCount = device.RentCount,
            Notes = device.Notes,
            ReservedUntil = device.ReservedUntil
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
            return NotFound();

        var hasRentals = await _context.RentalDevices.AnyAsync(rd => rd.DeviceId == id);
        var hasServices = await _context.ServiceDevices.AnyAsync(sd => sd.DeviceId == id);
        var hasFinancials = await _context.FinancialDevices.AnyAsync(fd => fd.DeviceId == id);

        if (hasRentals || hasServices || hasFinancials)
            return Conflict("Nem törölhető, mert vannak hozzá tartozó bérlések, szervizek vagy pénzügyi tételek.");

        DeleteImageFile(device.Picture);
        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<string?> SaveImage(IFormFile image)
    {
        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        if (!allowed.Contains(ext))
            return null;

        var fileName = $"{Guid.NewGuid()}{ext}";
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "devices");
        Directory.CreateDirectory(uploadsDir);
        var filePath = Path.Combine(uploadsDir, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await image.CopyToAsync(stream);

        return $"/uploads/devices/{fileName}";
    }

    private void DeleteImageFile(string? picturePath)
    {
        if (string.IsNullOrEmpty(picturePath) || !picturePath.StartsWith("/uploads/"))
            return;

        var filePath = Path.Combine(_env.WebRootPath, picturePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }
}
