using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ToolRental.Bikes;
using ToolRental.Core.Models;
using ToolRental.Data;

const string ProdMode = "prod";
const string TestMode = "test";

var bikeTypes = new[]
{
    "Férfi kerékpár",
    "Női kerékpár",
    "Férfi e-bike",
    "Női e-bike",
    "Gyerekbicikli",
    "Gyerekülés",
    "Utánfutó"
};

var builder = WebApplication.CreateBuilder(args);

var dataProtectionDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(dataProtectionDirectory);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory))
    .SetApplicationName("ToolRental.Bikes");

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton<RuntimeSqlSettingsStore>();
builder.Services.AddSingleton<SettingsFileStorage>();

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var sqlSettingsStore = scope.ServiceProvider.GetRequiredService<RuntimeSqlSettingsStore>();
    TryMigrateDatabase(sqlSettingsStore, ProdMode);
    TryMigrateDatabase(sqlSettingsStore, TestMode);
}

app.MapGet("/api/bikes/status", async (HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore) =>
{
    await using var db = CreateDbContext(sqlSettingsStore, GetRequestedMode(httpContext));
    var result = await BuildBikeStatusesAsync(db, bikeTypes);

    return Results.Json(new
    {
        databaseMode = GetRequestedMode(httpContext),
        items = result
    });
});

app.MapPost("/api/bikes/{id:int}/release", async (int id, HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore) =>
{
    await using var db = CreateDbContext(sqlSettingsStore, GetRequestedMode(httpContext));
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

app.MapPost("/api/bikes/{id:int}/reserve", async (int id, HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore) =>
{
    await using var db = CreateDbContext(sqlSettingsStore, GetRequestedMode(httpContext));
    var device = await db.Devices.FindAsync(id);
    if (device == null)
        return Results.NotFound(new { error = "Eszköz nem található." });

    var activeRental = await GetCurrentActiveRentalAsync(db, id);
    if (activeRental != null)
        return Results.BadRequest(new { error = "Kiadott eszközt nem lehet foglaltra állítani." });

    device.ReservedUntil = DateTime.Today;
    await db.SaveChangesAsync();

    return Results.Ok(new { success = true });
});

app.MapPost("/api/bikes/{id:int}/unreserve", async (int id, HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore) =>
{
    await using var db = CreateDbContext(sqlSettingsStore, GetRequestedMode(httpContext));
    var device = await db.Devices.FindAsync(id);
    if (device == null)
        return Results.NotFound(new { error = "Eszköz nem található." });

    device.ReservedUntil = null;
    await db.SaveChangesAsync();

    return Results.Ok(new { success = true });
});

app.MapGet("/api/bikes/image/{id:int}", async (int id, HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore) =>
{
    await using var db = CreateDbContext(sqlSettingsStore, GetRequestedMode(httpContext));
    var device = await db.Devices.FindAsync(id);
    if (device == null || string.IsNullOrEmpty(device.Picture))
        return Results.NotFound();

    var filePath = ResolvePicturePath(device.Picture);
    if (!File.Exists(filePath))
        return Results.NotFound();

    var contentTypeProvider = new FileExtensionContentTypeProvider();
    if (!contentTypeProvider.TryGetContentType(filePath, out var mimeType))
        mimeType = "application/octet-stream";

    return Results.File(File.OpenRead(filePath), mimeType);
});

app.MapGet("/api/settings", async (HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore, SettingsFileStorage settingsFileStorage) =>
{
    var mode = GetRequestedMode(httpContext);
    var sqlSettings = sqlSettingsStore.GetSettings();

    Setting? setting = null;
    var canConnect = false;
    string? dbMessage = null;

    try
    {
        await using var db = CreateDbContext(sqlSettingsStore, mode);
        canConnect = await db.Database.CanConnectAsync();
        if (canConnect)
            setting = await db.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
        else
            dbMessage = $"Nem sikerült kapcsolódni a {mode.ToUpperInvariant()} adatbázishoz.";
    }
    catch (Exception ex)
    {
        dbMessage = ex.Message;
    }

    var emailPasswordNeedsReset = setting?.EmailPassword.StartsWith("ENC:", StringComparison.Ordinal) == true;
    var emailPassword = emailPasswordNeedsReset ? string.Empty : setting?.EmailPassword ?? string.Empty;

    return Results.Json(new
    {
        mode,
        sql = sqlSettings,
        databaseStatus = new
        {
            canConnect,
            message = canConnect
                ? $"Kapcsolat sikeres a {mode.ToUpperInvariant()} adatbázishoz."
                : dbMessage ?? "Az adatbázis jelenleg nem érhető el."
        },
        application = new
        {
            companyName = string.IsNullOrWhiteSpace(setting?.CompanyName) ? "Kerékpár Bérlő Kft." : setting!.CompanyName,
            emailSmtp = setting?.EmailSmtp ?? string.Empty,
            smtpPort = setting?.SmtpPort ?? 587,
            senderEmail = setting?.SenderEmail ?? string.Empty,
            emailPassword,
            emailPasswordConfigured = !string.IsNullOrWhiteSpace(setting?.EmailPassword),
            emailPasswordNeedsReset,
            senderName = setting?.SenderName ?? string.Empty,
            ccAddress = setting?.CcAddress ?? string.Empty,
            emailSubject = string.IsNullOrWhiteSpace(setting?.EmailSubject) ? "Bérlési szerződés" : setting!.EmailSubject,
            reviewEmailSubject = string.IsNullOrWhiteSpace(setting?.ReviewEmailSubject) ? "Értékelje szolgáltatásunkat!" : setting!.ReviewEmailSubject,
            googleReview = setting?.GoogleReview ?? string.Empty,
            defaultRentalDays = setting?.DefaultRentalDays ?? 1,
            reviewEmailDelayDays = setting?.ReviewEmailDelayDays ?? 3
        },
        files = new
        {
            companyLogo = DescribeStoredFile("companyLogo", setting?.CompanyLogo, mode, settingsFileStorage),
            templateContract = DescribeStoredFile("templateContract", setting?.TemplateContract, mode, settingsFileStorage),
            aszfFile = DescribeStoredFile("aszfFile", setting?.AszfFile, mode, settingsFileStorage),
            contractEmailTemplate = DescribeStoredFile("contractEmailTemplate", setting?.ContractEmailTemplate, mode, settingsFileStorage),
            reviewEmailTemplate = DescribeStoredFile("reviewEmailTemplate", setting?.ReviewEmailTemplate, mode, settingsFileStorage),
            invoiceXml = DescribeStoredFile("invoiceXml", setting?.InvoiceXml, mode, settingsFileStorage)
        }
    });
});

app.MapPost("/api/settings/test-sql", async (HttpContext httpContext, SqlConnectionSettings request) =>
{
    var validationError = ValidateSqlSettings(request);
    if (validationError != null)
        return Results.BadRequest(new { error = validationError });

    var mode = GetRequestedMode(httpContext);
    try
    {
        await using var connection = new SqlConnection(BuildConnectionString(request, mode));
        await connection.OpenAsync();

        return Results.Ok(new
        {
            success = true,
            message = $"Kapcsolat sikeres a {mode.ToUpperInvariant()} adatbázishoz."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = $"Kapcsolat sikertelen: {ex.Message}"
        });
    }
});

app.MapPost("/api/settings", async (HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore, SettingsFileStorage settingsFileStorage) =>
{
    var mode = GetRequestedMode(httpContext);
    var form = await httpContext.Request.ReadFormAsync();

    var sqlSettings = new SqlConnectionSettings
    {
        Server = GetRequiredTrimmed(form, "sqlServer"),
        Port = ParseIntOrDefault(form["sqlPort"], 1433, minValue: 1),
        Database = GetRequiredTrimmed(form, "sqlDatabase"),
        UserId = GetRequiredTrimmed(form, "sqlUserId"),
        Password = form["sqlPassword"].ToString(),
        TrustServerCertificate = ParseBool(form["sqlTrustServerCertificate"]),
        TestDatabaseName = string.IsNullOrWhiteSpace(form["testDatabaseName"])
            ? "ToolRentalDB_Test"
            : form["testDatabaseName"].ToString().Trim()
    };

    var validationError = ValidateSqlSettings(sqlSettings);
    if (validationError != null)
        return Results.BadRequest(new { error = validationError });

    try
    {
        await using var connection = new SqlConnection(BuildConnectionString(sqlSettings, mode));
        await connection.OpenAsync();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Kapcsolat sikertelen: {ex.Message}" });
    }

    sqlSettingsStore.SaveSettings(sqlSettings);

    try
    {
        await using var db = CreateDbContext(sqlSettingsStore, mode);
        await db.Database.MigrateAsync();

        var setting = await db.Settings.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (setting == null)
        {
            setting = new Setting();
            db.Settings.Add(setting);
        }

        setting.CompanyName = string.IsNullOrWhiteSpace(form["companyName"])
            ? "Kerékpár Bérlő Kft."
            : form["companyName"].ToString().Trim();

        setting.EmailSmtp = form["emailSmtp"].ToString().Trim();
        setting.SmtpPort = ParseIntOrDefault(form["smtpPort"], 587, minValue: 1);
        setting.SenderEmail = form["senderEmail"].ToString().Trim();
        setting.SenderName = form["senderName"].ToString().Trim();
        setting.CcAddress = NullIfWhiteSpace(form["ccAddress"]);
        setting.EmailSubject = string.IsNullOrWhiteSpace(form["emailSubject"])
            ? "Bérlési szerződés"
            : form["emailSubject"].ToString().Trim();
        setting.ReviewEmailSubject = string.IsNullOrWhiteSpace(form["reviewEmailSubject"])
            ? "Értékelje szolgáltatásunkat!"
            : form["reviewEmailSubject"].ToString().Trim();
        setting.GoogleReview = NullIfWhiteSpace(form["googleReview"]);
        setting.DefaultRentalDays = ParseIntOrDefault(form["defaultRentalDays"], 1, minValue: 1);
        setting.ReviewEmailDelayDays = ParseIntOrDefault(form["reviewEmailDelayDays"], 3, minValue: 0);

        var postedEmailPassword = form["emailPassword"].ToString();
        if (!string.IsNullOrWhiteSpace(postedEmailPassword))
            setting.EmailPassword = postedEmailPassword;
        else if (ParseBool(form["clearEmailPassword"]))
            setting.EmailPassword = string.Empty;

        await UpdateStoredFileAsync(setting, "companyLogo", form.Files.GetFile("companyLogo"), ParseBool(form["clearCompanyLogo"]), settingsFileStorage);
        await UpdateStoredFileAsync(setting, "templateContract", form.Files.GetFile("templateContract"), ParseBool(form["clearTemplateContract"]), settingsFileStorage);
        await UpdateStoredFileAsync(setting, "aszfFile", form.Files.GetFile("aszfFile"), ParseBool(form["clearAszfFile"]), settingsFileStorage);
        await UpdateStoredFileAsync(setting, "contractEmailTemplate", form.Files.GetFile("contractEmailTemplate"), ParseBool(form["clearContractEmailTemplate"]), settingsFileStorage);
        await UpdateStoredFileAsync(setting, "reviewEmailTemplate", form.Files.GetFile("reviewEmailTemplate"), ParseBool(form["clearReviewEmailTemplate"]), settingsFileStorage);
        await UpdateStoredFileAsync(setting, "invoiceXml", form.Files.GetFile("invoiceXml"), ParseBool(form["clearInvoiceXml"]), settingsFileStorage);

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            success = true,
            message = $"A beállítások sikeresen elmentve a {mode.ToUpperInvariant()} adatbázishoz."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = $"A beállítások mentése közben hiba történt: {ex.Message}"
        });
    }
});

app.MapGet("/api/settings/files/{fileKey}", async (string fileKey, HttpContext httpContext, RuntimeSqlSettingsStore sqlSettingsStore, SettingsFileStorage settingsFileStorage) =>
{
    var mode = GetRequestedMode(httpContext);

    await using var db = CreateDbContext(sqlSettingsStore, mode);
    var setting = await db.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
    if (setting == null)
        return Results.NotFound();

    var storedPath = GetStoredFilePath(setting, fileKey);
    if (string.IsNullOrWhiteSpace(storedPath) || !settingsFileStorage.IsManagedFile(storedPath) || !File.Exists(storedPath))
        return Results.NotFound();

    var contentTypeProvider = new FileExtensionContentTypeProvider();
    if (!contentTypeProvider.TryGetContentType(storedPath, out var mimeType))
        mimeType = "application/octet-stream";

    return Results.File(File.OpenRead(storedPath), mimeType, Path.GetFileName(storedPath), enableRangeProcessing: false);
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run("http://0.0.0.0:3001");

static void TryMigrateDatabase(RuntimeSqlSettingsStore sqlSettingsStore, string mode)
{
    try
    {
        using var db = CreateDbContext(sqlSettingsStore, mode);
        db.Database.Migrate();
    }
    catch
    {
        // Ha a kapcsolat nincs még beállítva vagy a DB nem érhető el, a webapp ettől még induljon el.
    }
}

static string ResolvePicturePath(string? dbPath)
{
    if (string.IsNullOrEmpty(dbPath))
        return string.Empty;

    var normalized = dbPath.Replace('\\', '/');
    if (normalized.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
        normalized = "/srv/samba/telihold/" + normalized[3..];

    return normalized;
}

static string GetRequestedMode(HttpContext httpContext)
{
    var requested = httpContext.Request.Query["db"].ToString();
    return string.Equals(requested, TestMode, StringComparison.OrdinalIgnoreCase)
        ? TestMode
        : ProdMode;
}

static ToolRentalDbContext CreateDbContext(RuntimeSqlSettingsStore sqlSettingsStore, string mode)
{
    var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
    optionsBuilder.UseSqlServer(
        sqlSettingsStore.BuildConnectionString(mode),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null));

    return new ToolRentalDbContext(optionsBuilder.Options);
}

static string BuildConnectionString(SqlConnectionSettings settings, string mode)
{
    var databaseName = string.Equals(mode, TestMode, StringComparison.OrdinalIgnoreCase)
        ? (string.IsNullOrWhiteSpace(settings.TestDatabaseName) ? settings.Database : settings.TestDatabaseName)
        : settings.Database;

    var builder = new SqlConnectionStringBuilder
    {
        DataSource = $"{settings.Server},{settings.Port}",
        InitialCatalog = databaseName,
        UserID = settings.UserId,
        Password = settings.Password,
        TrustServerCertificate = settings.TrustServerCertificate,
        ConnectTimeout = 5
    };

    return builder.ConnectionString;
}

static string? ValidateSqlSettings(SqlConnectionSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.Server))
        return "Az SQL szerver neve kötelező.";

    if (settings.Port <= 0)
        return "Érvényes SQL port megadása kötelező.";

    if (string.IsNullOrWhiteSpace(settings.Database))
        return "Az adatbázis neve kötelező.";

    if (string.IsNullOrWhiteSpace(settings.UserId))
        return "Az SQL felhasználónév kötelező.";

    if (string.IsNullOrWhiteSpace(settings.Password))
        return "Az SQL jelszó kötelező.";

    return null;
}

static string GetRequiredTrimmed(IFormCollection form, string key)
{
    return form[key].ToString().Trim();
}

static int ParseIntOrDefault(string? value, int fallback, int minValue)
{
    return int.TryParse(value, out var parsed) && parsed >= minValue
        ? parsed
        : fallback;
}

static bool ParseBool(string? value)
{
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
}

static string? NullIfWhiteSpace(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static async Task UpdateStoredFileAsync(Setting setting, string fileKey, IFormFile? file, bool clearExisting, SettingsFileStorage settingsFileStorage)
{
    var currentPath = GetStoredFilePath(setting, fileKey);
    if (clearExisting)
    {
        settingsFileStorage.TryDeleteManagedFile(currentPath);
        SetStoredFilePath(setting, fileKey, null);
    }

    if (file == null || file.Length <= 0)
        return;

    var allowedExtensions = GetAllowedExtensions(fileKey);
    var savedPath = await settingsFileStorage.SaveAsync(file, fileKey, allowedExtensions);

    settingsFileStorage.TryDeleteManagedFile(currentPath);
    SetStoredFilePath(setting, fileKey, savedPath);
}

static string[] GetAllowedExtensions(string fileKey)
{
    return fileKey switch
    {
        "companyLogo" => [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".svg"],
        "templateContract" => [".docx"],
        "aszfFile" => [".pdf"],
        "contractEmailTemplate" => [".html", ".htm"],
        "reviewEmailTemplate" => [".html", ".htm"],
        "invoiceXml" => [".xml"],
        _ => throw new InvalidOperationException("Ismeretlen fájltípus.")
    };
}

static string? GetStoredFilePath(Setting setting, string fileKey)
{
    return fileKey switch
    {
        "companyLogo" => setting.CompanyLogo,
        "templateContract" => setting.TemplateContract,
        "aszfFile" => setting.AszfFile,
        "contractEmailTemplate" => setting.ContractEmailTemplate,
        "reviewEmailTemplate" => setting.ReviewEmailTemplate,
        "invoiceXml" => setting.InvoiceXml,
        _ => throw new InvalidOperationException("Ismeretlen fájlkulcs.")
    };
}

static void SetStoredFilePath(Setting setting, string fileKey, string? value)
{
    switch (fileKey)
    {
        case "companyLogo":
            setting.CompanyLogo = value;
            return;
        case "templateContract":
            setting.TemplateContract = value;
            return;
        case "aszfFile":
            setting.AszfFile = value;
            return;
        case "contractEmailTemplate":
            setting.ContractEmailTemplate = value;
            return;
        case "reviewEmailTemplate":
            setting.ReviewEmailTemplate = value;
            return;
        case "invoiceXml":
            setting.InvoiceXml = value;
            return;
        default:
            throw new InvalidOperationException("Ismeretlen fájlkulcs.");
    }
}

static StoredFileDescriptor DescribeStoredFile(string fileKey, string? storedPath, string mode, SettingsFileStorage settingsFileStorage)
{
    if (string.IsNullOrWhiteSpace(storedPath))
    {
        return new StoredFileDescriptor(fileKey, string.Empty, string.Empty, false, false, null, null, "Nincs fájl feltöltve.");
    }

    var exists = File.Exists(storedPath);
    var canServe = exists && settingsFileStorage.IsManagedFile(storedPath);
    var isImage = fileKey == "companyLogo";
    var url = canServe ? $"/api/settings/files/{fileKey}?db={mode}" : null;
    var status = exists
        ? (canServe ? "Fájl elérhető a szerveren." : "A fájl megvan, de nem webes feltöltésből származik.")
        : "A korábban mentett fájl ezen a szerveren nem található.";

    return new StoredFileDescriptor(
        fileKey,
        Path.GetFileName(storedPath),
        storedPath,
        exists,
        canServe,
        url,
        isImage ? url : null,
        status);
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

    var releases = await db.BikeReleases
        .Where(br => deviceIds.Contains(br.DeviceId) && br.ReleaseDate <= today)
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
        var isOccupied = currentRental != null;
        var isReserved = !isOccupied && device.ReservedUntil.HasValue && device.ReservedUntil.Value.Date >= today;

        result.Add(new
        {
            id = device.Id,
            name = device.DeviceName,
            typeName = device.DeviceTypeNavigation?.TypeName ?? string.Empty,
            isOccupied,
            isReserved,
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

static List<ActiveRentalRow> ResolveUnclosedRentals(IEnumerable<ActiveRentalRow> rentals, IEnumerable<BikeReleaseRow> releases, DateTime today)
{
    var orderedRentals = rentals
        .OrderBy(r => r.RentStart)
        .ThenBy(r => r.RentalId)
        .ToList();

    if (orderedRentals.Count == 0)
        return orderedRentals;

    var firstActiveRentalDate = orderedRentals.First().RentStart.Date;
    var closureCount = releases.Count(r => r.ReleaseDate.Date >= firstActiveRentalDate && r.ReleaseDate.Date <= today);
    return orderedRentals.Skip(Math.Min(closureCount, orderedRentals.Count)).ToList();
}

static bool IsRentalActiveToday(ActiveRentalRow rental, DateTime today)
{
    var rentalStartDate = rental.RentStart.Date;
    var rentalEndExclusive = rentalStartDate.AddDays(rental.RentalDays);
    return today >= rentalStartDate && today < rentalEndExclusive;
}

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

internal sealed record StoredFileDescriptor(
    string Key,
    string FileName,
    string StoredPath,
    bool Exists,
    bool CanServe,
    string? DownloadUrl,
    string? PreviewUrl,
    string Status);
