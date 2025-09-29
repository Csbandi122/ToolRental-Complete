using Microsoft.EntityFrameworkCore;
using ToolRental.Data;

var builder = WebApplication.CreateBuilder(args);

// --- Ezt a részt adjuk hozzá ---
// Adatbázis kapcsolat beállítása az appsettings.json alapján
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ToolRentalDbContext>(options =>
    options.UseSqlite(connectionString));
// --- Eddig a részig ---

// Add services to the container.
// EZ AZ ÚJ RÉSZ, AMI MEGOLDJA A CIKLUS HIBÁT
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
// EZT A SORT MÁR ISMERED:
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ez az alapértelmezett "időjárás" végpont, ezt ki is törölheted, de maradhat
app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            "Sample"
        ))
        .ToArray();
    return forecast;
});



// --- Story 1.1: Ide jön az új kódunk az ügyfelek lekérdezéséhez ---
app.MapGet("/api/customers", async (ToolRentalDbContext db) =>
{
    var customers = await db.Customers.ToListAsync();
    return Results.Ok(customers);
});
// --- Kód vége ---




// Story 1.2: Eszközök lekérdezése (JAVÍTOTT VERZIÓ)
app.MapGet("/api/devices", async (ToolRentalDbContext db) =>
{
    var devices = await db.Devices.Include(d => d.DeviceTypeNavigation).ToListAsync();
    return Results.Ok(devices);
});

app.Run();

// Ezt a rekordot a fájl legalján kell hagyni, ha a WeatherForecast végpont megmaradt
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}