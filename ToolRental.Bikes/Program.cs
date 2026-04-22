using Microsoft.EntityFrameworkCore;
using ToolRental.Core.Models;
using ToolRental.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ToolRentalDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// CORS - mobil eszközök is elérhessék
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// Futtatáskor automatikusan alkalmazza a pending migration-öket (BikeReleases tábla létrehozása)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ToolRentalDbContext>();
    try { db.Database.Migrate(); } catch { /* Ha már fut, vagy nincs jog, folytassuk */ }
}

// === ESZKÖZ TÍPUSOK, amelyek megjelennek a dashboardon ===
var bikeTypes = new[]
{
    "Férfi kerékpár", "Női kerékpár",
    "Férfi e-bike", "Női e-bike",
    "Gyerekbicikli", "Gyerekülés", "Utánfutó"
};

// === BIKES STATUS API ===
// Visszaadja az összes megjelenítendő eszközt és hogy foglalt-e ma
app.MapGet("/api/bikes/status", async (ToolRentalDbContext db) =>
{
    var today = DateTime.Today;

    var devices = await db.Devices
        .Include(d => d.DeviceTypeNavigation)
        .Where(d => d.Available && d.DeviceTypeNavigation != null && bikeTypes.Contains(d.DeviceTypeNavigation.TypeName))
        .OrderBy(d => d.DeviceTypeNavigation!.TypeName)
        .ThenBy(d => d.DeviceName)
        .ToListAsync();

    // Mai bérlések száma per eszköz
    var todayRentals = await db.RentalDevices
        .Include(rd => rd.Rental)
        .Where(rd => rd.Rental != null && rd.Rental.RentStart.Date == today)
        .GroupBy(rd => rd.DeviceId)
        .Select(g => new { DeviceId = g.Key, Count = g.Count() })
        .ToListAsync();

    // Mai felszabadítások száma per eszköz
    var todayReleases = await db.BikeReleases
        .Where(br => br.ReleaseDate.Date == today)
        .GroupBy(br => br.DeviceId)
        .Select(g => new { DeviceId = g.Key, Count = g.Count() })
        .ToListAsync();

    var rentalMap = todayRentals.ToDictionary(r => r.DeviceId, r => r.Count);
    var releaseMap = todayReleases.ToDictionary(r => r.DeviceId, r => r.Count);

    var result = devices.Select(d =>
    {
        var rentals = rentalMap.GetValueOrDefault(d.Id, 0);
        var releases = releaseMap.GetValueOrDefault(d.Id, 0);
        var isOccupied = (rentals - releases) > 0;

        return new
        {
            id = d.Id,
            name = d.DeviceName,
            typeName = d.DeviceTypeNavigation?.TypeName ?? "",
            isOccupied,
            rentalsToday = rentals,
            releasesToday = releases,
            hasImage = !string.IsNullOrEmpty(d.Picture) && File.Exists(ResolvePicturePath(d.Picture))
        };
    });

    return Results.Json(result);
});

// === FELSZABADÍTÁS API ===
// Hozzáad egy release rekordot az eszközhöz a mai napra
app.MapPost("/api/bikes/{id:int}/release", async (int id, ToolRentalDbContext db) =>
{
    var device = await db.Devices.FindAsync(id);
    if (device == null)
        return Results.NotFound(new { error = "Eszköz nem található." });

    db.BikeReleases.Add(new BikeRelease
    {
        DeviceId = id,
        ReleaseDate = DateTime.Today
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
});

// === KÉP KISZOLGÁLÁS ===
// Az adatbázisban tárolt fájlrendszeri elérési útból kiszolgálja a képet
// Windows path (Z:\Sablonok\...) → Linux path (/srv/samba/telihold/Sablonok/...) fordítás
static string ResolvePicturePath(string? dbPath)
{
    if (string.IsNullOrEmpty(dbPath)) return "";
    // Windows Samba elérési út fordítása Linux path-ra
    var normalized = dbPath.Replace('\\', '/');
    if (normalized.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
        normalized = "/srv/samba/telihold/" + normalized[3..];
    return normalized;
}

app.MapGet("/api/bikes/image/{id:int}", async (int id, ToolRentalDbContext db) =>
{
    var device = await db.Devices.FindAsync(id);
    if (device == null || string.IsNullOrEmpty(device.Picture))
        return Results.NotFound();

    var filePath = ResolvePicturePath(device.Picture);
    if (!File.Exists(filePath))
        return Results.NotFound();

    var ext = Path.GetExtension(filePath).ToLowerInvariant();
    var mimeType = ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream"
    };

    return Results.File(File.OpenRead(filePath), mimeType);
});

// Főoldal → index.html
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run("http://0.0.0.0:3001");
