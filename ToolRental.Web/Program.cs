using Microsoft.EntityFrameworkCore;
using ToolRental.Core;
using ToolRental.Core.Models;
using ToolRental.Data;

var builder = WebApplication.CreateBuilder(args);

// SQL Server kapcsolat az appsettings.json-ból
builder.Services.AddDbContext<ToolRentalDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

var app = builder.Build();

// Statikus fájlok kiszolgálása (CSS, JS, stb.)
app.UseStaticFiles();

// === REPORTING API ===
app.MapGet("/api/reporting", async (ToolRentalDbContext db) =>
{
    var today = DateTime.Today;
    var weekStart = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1)); // Hétfő
    var monthStart = new DateTime(today.Year, today.Month, 1);

    // Mai nap
    var todayRevenue = await db.Financials
        .Where(f => f.Date.Date == today && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var todayExpense = await db.Financials
        .Where(f => f.Date.Date == today && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Aktuális hét (hétfőtől)
    var weekRevenue = await db.Financials
        .Where(f => f.Date.Date >= weekStart && f.Date.Date <= today && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var weekExpense = await db.Financials
        .Where(f => f.Date.Date >= weekStart && f.Date.Date <= today && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Aktuális hónap
    var monthRevenue = await db.Financials
        .Where(f => f.Date >= monthStart && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var monthExpense = await db.Financials
        .Where(f => f.Date >= monthStart && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Aktuális év
    var yearStart = new DateTime(today.Year, 1, 1);
    var yearRevenue = await db.Financials
        .Where(f => f.Date >= yearStart && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var yearExpense = await db.Financials
        .Where(f => f.Date >= yearStart && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    return Results.Json(new
    {
        generatedAt = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"),
        today = new { revenue = todayRevenue, expense = todayExpense, profit = todayRevenue - todayExpense },
        week = new { revenue = weekRevenue, expense = weekExpense, profit = weekRevenue - weekExpense, startDate = weekStart.ToString("yyyy.MM.dd") },
        month = new { revenue = monthRevenue, expense = monthExpense, profit = monthRevenue - monthExpense, startDate = monthStart.ToString("yyyy.MM.dd") },
        year = new { revenue = yearRevenue, expense = yearExpense, profit = yearRevenue - yearExpense, startDate = yearStart.ToString("yyyy.MM.dd") }
    });
});

// Főoldal → reporting.html
app.MapGet("/", () => Results.Redirect("/reporting.html"));

// Hálózaton elérhető legyen (nem csak localhost)
app.Run("http://0.0.0.0:5000");
