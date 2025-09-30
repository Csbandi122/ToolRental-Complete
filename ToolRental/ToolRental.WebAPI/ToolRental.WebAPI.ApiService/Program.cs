using Microsoft.EntityFrameworkCore;
using ToolRental.Core.Models;
using ToolRental.Data;
using ToolRental.WebAPI.ApiService.Dtos; // EZ AZ ÚJ SOR!

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



/// CSERÉLD LE A TELJES /api/rentals BLOKKOT ERRE A VÉGLEGES VERZIÓRA:

// Story 1.3: Bérlések lekérdezése (DTO-kkal - VÉGLEGES)
app.MapGet("/api/rentals", async (ToolRentalDbContext db) =>
{
    var rentalsFromDb = await db.Rentals
        .Include(r => r.Customer)
        .Include(r => r.RentalDevices)
            .ThenInclude(rd => rd.Device)
        .ToListAsync();

    // Átalakítás DTO-kká a te modelljeid alapján
    var rentalDtos = rentalsFromDb.Select(r => new RentalDto
    {
        Id = r.Id,
        TicketNr = r.TicketNr,
        RentStart = r.RentStart,      // JAVÍTVA
        RentalDays = r.RentalDays,
        TotalAmount = r.TotalAmount,  // JAVÍTVA
        Customer = new CustomerDto { Name = r.Customer.Name },
        Devices = r.RentalDevices.Select(rd => new DeviceDto { DeviceName = rd.Device.DeviceName }).ToList()
    }).ToList();

    return Results.Ok(rentalDtos);
});



// Story 3.2: Új ügyfél mentése
app.MapPost("/api/customers", async (Customer customer, ToolRentalDbContext db) =>
{
    db.Customers.Add(customer);
    await db.SaveChangesAsync();
    return Results.Created($"/api/customers/{customer.Id}", customer);
});

// Az applikáció futtatása
app.Run();