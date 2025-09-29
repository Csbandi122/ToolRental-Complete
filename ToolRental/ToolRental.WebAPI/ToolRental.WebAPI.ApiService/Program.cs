using Microsoft.EntityFrameworkCore;
using ToolRental.Data;

var builder = WebApplication.CreateBuilder(args);

// Adatbázis kapcsolat beállítása az appsettings.json alapján
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ToolRentalDbContext>(options =>
    options.UseSqlite(connectionString));

// --- Szolgáltatások beállítása ---

// JSON cikluskezelés beállítása (EZ A FONTOS RÉSZ!)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Swagger/OpenAPI beállítása
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Applikáció felépítése ---
var app = builder.Build();

// HTTP pipeline beállítása
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// === API Végpontok (Endpoints) ===

// Story 1.1: Ügyfelek lekérdezése
app.MapGet("/api/customers", async (ToolRentalDbContext db) =>
{
    var customers = await db.Customers.ToListAsync();
    return Results.Ok(customers);
});

// Story 1.2: Eszközök lekérdezése
app.MapGet("/api/devices", async (ToolRentalDbContext db) =>
{
    var devices = await db.Devices.Include(d => d.DeviceTypeNavigation).ToListAsync();
    return Results.Ok(devices);
});

// CSERÉLD LE A TELJES /api/rentals BLOKKOT ERRE AZ ÚJ VERZIÓRA:

// Story 1.3: Bérlések lekérdezése (ÚJ HIBAKERESŐ VERZIÓ)
app.MapGet("/api/rentals", async (ToolRentalDbContext db) =>
{
    var rentals = await db.Rentals
        .Include(r => r.Customer)
        .Include(r => r.RentalDevices)
            .ThenInclude(rd => rd.Device)
        .ToListAsync();

    // Kézzel létrehozzuk a beállításokat, csak ehhez a végponthoz
    var options = new System.Text.Json.JsonSerializerOptions
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    // A Results.Ok helyett a Results.Json-t használjuk a saját beállításainkkal
    return Results.Json(rentals, options);
});

// Az applikáció futtatása
app.Run();