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

// Futtatáskor automatikusan alkalmazza a pending migration-öket
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
// Aktív bérlés = a bérlési időszak még tart, és nem lett kézzel lezárva.
app.MapGet("/api/bikes/status", async (ToolRentalDbContext db) =>
{
    var result = await BuildBikeStatusesAsync(db, bikeTypes);
    return Results.Json(result);
});

// === BÉRLÉS LEZÁRÁS API ===
// Kézzel lezárja az aktuálisan aktív bérlést, hogy az eszköz még aznap újra kiadható legyen.
app.MapPost("/api/bikes/{id:int}/release", async (int id, ToolRentalDbContext db) =>
{
    var device = await db.Devices.FindAsync(id);
    if (device == null)
        return Results.NotFound(new { error = "Eszköz nem található." });

    var activeRental = await GetCurrentActiveRentalAsync(db, id);
    if (activeRental == null)
        return Results.BadRequest(new { error = "Ehhez az eszközhöz jelenleg nincs aktív bérlés." });

    db.BikeReleases.Add(new BikeRelease
    {
        DeviceId = id,
        ReleaseDate = DateTime.Today
    });

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        success = true,
        rentalId = activeRental.RentalId,
        ticketNr = activeRental.TicketNr
    });
});

// === KÉP KISZOLGÁLÁS ===
// Az adatbázisban tárolt fájlrendszeri elérési útból kiszolgálja a képet
// Windows path (Z:\Sablonok\...) → Linux path (/srv/samba/telihold/Sablonok/...) fordítás
static string ResolvePicturePath(string? dbPath)
{
    if (string.IsNullOrEmpty(dbPath)) return "";

    var normalized = dbPath.Replace('\\', '/');
    if (normalized.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
        normalized = "/srv/samba/telihold/" + normalized[3..];

    return normalized;
}

static async Task<List<object>> BuildBikeStatusesAsync(ToolRentalDbContext db, string[] bikeTypes)
{
    var today = DateTime.Today;

    var devices = await db.Devices
        .Include(d => d.DeviceTypeNavigation)
        .Where(d => d.Available && d.DeviceTypeNavigation != null && bikeTypes.Contains(d.DeviceTypeNavigation.TypeName))
        .OrderBy(d => d.DeviceTypeNavigation!.TypeName)
        .ThenBy(d => d.DeviceName)
        .ToListAsync();

    if (devices.Count == 0)
        return new List<object>();

    var deviceIds = devices.Select(d => d.Id).ToList();

    var rentals = await db.RentalDevices
        .Where(rd => deviceIds.Contains(rd.DeviceId))
        .Select(rd => new ActiveRentalRow(
            rd.DeviceId,
            rd.RentalId,
            rd.Rental.TicketNr,
            rd.Rental.RentStart,
            rd.Rental.RentalDays,
            rd.Rental.Customer.Name))
        .ToListAsync();

    var activeRentals = rentals
        .Where(r => IsRentalActiveToday(r, today))
        .OrderBy(r => r.RentStart)
        .ThenBy(r => r.RentalId)
        .ToList();

    var releasesQuery = db.BikeReleases
        .Where(br => deviceIds.Contains(br.DeviceId) && br.ReleaseDate <= today);

    var releases = await releasesQuery
        .Select(br => new BikeReleaseRow(br.DeviceId, br.ReleaseDate))
        .ToListAsync();

    var result = new List<object>(devices.Count);

    foreach (var device in devices)
    {
        var unresolvedRentals = ResolveUnclosedRentals(
            activeRentals.Where(r => r.DeviceId == device.Id),
            releases.Where(r => r.DeviceId == device.Id),
            today);

        var currentRental = unresolvedRentals.LastOrDefault();

        result.Add(new
        {
            id = device.Id,
            name = device.DeviceName,
            typeName = device.DeviceTypeNavigation?.TypeName ?? "",
            isOccupied = currentRental != null,
            activeRentalCount = unresolvedRentals.Count,
            currentRentalId = currentRental?.RentalId,
            currentTicketNr = currentRental?.TicketNr,
            currentCustomerName = currentRental?.CustomerName,
            rentStartDate = currentRental?.RentStart.ToString("yyyy.MM.dd"),
            plannedEndDate = currentRental == null
                ? null
                : currentRental.RentStart.Date.AddDays(currentRental.RentalDays - 1).ToString("yyyy.MM.dd"),
            hasImage = !string.IsNullOrEmpty(device.Picture) && File.Exists(ResolvePicturePath(device.Picture))
        });
    }

    return result;
}

static async Task<ActiveRentalRow?> GetCurrentActiveRentalAsync(ToolRentalDbContext db, int deviceId)
{
    var today = DateTime.Today;

    var rentals = await db.RentalDevices
        .Where(rd => rd.DeviceId == deviceId)
        .Select(rd => new ActiveRentalRow(
            rd.DeviceId,
            rd.RentalId,
            rd.Rental.TicketNr,
            rd.Rental.RentStart,
            rd.Rental.RentalDays,
            rd.Rental.Customer.Name))
        .ToListAsync();

    var activeRentals = rentals
        .Where(r => IsRentalActiveToday(r, today))
        .OrderBy(r => r.RentStart)
        .ThenBy(r => r.RentalId)
        .ToList();

    if (activeRentals.Count == 0)
        return null;

    var releases = await db.BikeReleases
        .Where(br => br.DeviceId == deviceId && br.ReleaseDate <= today)
        .Select(br => new BikeReleaseRow(br.DeviceId, br.ReleaseDate))
        .ToListAsync();

    return ResolveUnclosedRentals(activeRentals, releases, today).LastOrDefault();
}

static List<ActiveRentalRow> ResolveUnclosedRentals(
    IEnumerable<ActiveRentalRow> rentals,
    IEnumerable<BikeReleaseRow> releases,
    DateTime today)
{
    var orderedRentals = rentals
        .OrderBy(r => r.RentStart)
        .ThenBy(r => r.RentalId)
        .ToList();

    if (orderedRentals.Count == 0)
        return orderedRentals;

    var firstActiveRentalDate = orderedRentals.First().RentStart.Date;
    var closureCount = releases.Count(r => r.ReleaseDate.Date >= firstActiveRentalDate && r.ReleaseDate.Date <= today);
    var unresolved = orderedRentals.Skip(Math.Min(closureCount, orderedRentals.Count)).ToList();

    return unresolved;
}

static bool IsRentalActiveToday(ActiveRentalRow rental, DateTime today)
{
    var rentalStartDate = rental.RentStart.Date;
    var rentalEndExclusive = rentalStartDate.AddDays(rental.RentalDays);
    return today >= rentalStartDate && today < rentalEndExclusive;
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

internal sealed record ActiveRentalRow(
    int DeviceId,
    int RentalId,
    string TicketNr,
    DateTime RentStart,
    int RentalDays,
    string CustomerName);

internal sealed record BikeReleaseRow(
    int DeviceId,
    DateTime ReleaseDate);
