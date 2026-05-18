using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ToolRental.Core.Models;
using ToolRental.Data;
using ToolRental.WebApp.Infrastructure;

namespace ToolRental.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly RuntimeSqlSettingsStore _runtimeSqlSettingsStore;
    private readonly SettingsFileStorage _settingsFileStorage;

    public SettingsController(RuntimeSqlSettingsStore runtimeSqlSettingsStore, SettingsFileStorage settingsFileStorage)
    {
        _runtimeSqlSettingsStore = runtimeSqlSettingsStore;
        _settingsFileStorage = settingsFileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var sqlSettings = _runtimeSqlSettingsStore.GetSettings();

        Setting? setting = null;
        var canConnect = false;
        string? message = null;

        try
        {
            await using var db = CreateDbContext();
            canConnect = await db.Database.CanConnectAsync();
            if (canConnect)
            {
                setting = await db.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
                message = "Kapcsolat sikeres az adatbázissal.";
            }
            else
            {
                message = "Az adatbázis jelenleg nem érhető el.";
            }
        }
        catch (Exception ex)
        {
            message = ex.Message;
        }

        var emailPasswordNeedsReset = setting?.EmailPassword.StartsWith("ENC:", StringComparison.Ordinal) == true;
        var emailPassword = emailPasswordNeedsReset ? string.Empty : setting?.EmailPassword ?? string.Empty;

        return Ok(new
        {
            sql = sqlSettings,
            databaseStatus = new
            {
                canConnect,
                message
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
                companyLogo = DescribeStoredFile("companyLogo", setting?.CompanyLogo),
                templateContract = DescribeStoredFile("templateContract", setting?.TemplateContract),
                aszfFile = DescribeStoredFile("aszfFile", setting?.AszfFile),
                contractEmailTemplate = DescribeStoredFile("contractEmailTemplate", setting?.ContractEmailTemplate),
                reviewEmailTemplate = DescribeStoredFile("reviewEmailTemplate", setting?.ReviewEmailTemplate),
                invoiceXml = DescribeStoredFile("invoiceXml", setting?.InvoiceXml)
            }
        });
    }

    [HttpPost("test-sql")]
    public async Task<IActionResult> TestSqlConnection([FromBody] SqlConnectionSettings settings)
    {
        var validationError = ValidateSqlSettings(settings);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        try
        {
            await using var connection = new SqlConnection(_runtimeSqlSettingsStore.BuildConnectionString(settings));
            await connection.OpenAsync();

            return Ok(new
            {
                success = true,
                message = "Kapcsolat sikeres az adatbázissal."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = $"Kapcsolat sikertelen: {ex.Message}"
            });
        }
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Save()
    {
        var form = await Request.ReadFormAsync();

        var sqlSettings = new SqlConnectionSettings
        {
            Server = form["sqlServer"].ToString().Trim(),
            Port = ParseIntOrDefault(form["sqlPort"], 1433, 1),
            Database = form["sqlDatabase"].ToString().Trim(),
            UserId = form["sqlUserId"].ToString().Trim(),
            Password = form["sqlPassword"].ToString(),
            TrustServerCertificate = ParseBool(form["sqlTrustServerCertificate"])
        };

        var validationError = ValidateSqlSettings(sqlSettings);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        try
        {
            await using var connection = new SqlConnection(_runtimeSqlSettingsStore.BuildConnectionString(sqlSettings));
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Kapcsolat sikertelen: {ex.Message}" });
        }

        _runtimeSqlSettingsStore.SaveSettings(sqlSettings);

        try
        {
            await using var db = CreateDbContext();
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
            setting.SmtpPort = ParseIntOrDefault(form["smtpPort"], 587, 1);
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
            setting.DefaultRentalDays = ParseIntOrDefault(form["defaultRentalDays"], 1, 1);
            setting.ReviewEmailDelayDays = ParseIntOrDefault(form["reviewEmailDelayDays"], 3, 0);

            var emailPassword = form["emailPassword"].ToString();
            if (!string.IsNullOrWhiteSpace(emailPassword))
                setting.EmailPassword = emailPassword;
            else if (ParseBool(form["clearEmailPassword"]))
                setting.EmailPassword = string.Empty;

            await UpdateStoredFileAsync(setting, "companyLogo", form.Files.GetFile("companyLogo"), ParseBool(form["clearCompanyLogo"]));
            await UpdateStoredFileAsync(setting, "templateContract", form.Files.GetFile("templateContract"), ParseBool(form["clearTemplateContract"]));
            await UpdateStoredFileAsync(setting, "aszfFile", form.Files.GetFile("aszfFile"), ParseBool(form["clearAszfFile"]));
            await UpdateStoredFileAsync(setting, "contractEmailTemplate", form.Files.GetFile("contractEmailTemplate"), ParseBool(form["clearContractEmailTemplate"]));
            await UpdateStoredFileAsync(setting, "reviewEmailTemplate", form.Files.GetFile("reviewEmailTemplate"), ParseBool(form["clearReviewEmailTemplate"]));
            await UpdateStoredFileAsync(setting, "invoiceXml", form.Files.GetFile("invoiceXml"), ParseBool(form["clearInvoiceXml"]));

            await db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "A beállítások sikeresen elmentve."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = $"A beállítások mentése közben hiba történt: {ex.Message}"
            });
        }
    }

    [HttpGet("files/{fileKey}")]
    public async Task<IActionResult> GetFile(string fileKey)
    {
        await using var db = CreateDbContext();
        var setting = await db.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (setting == null)
            return NotFound();

        var storedPath = GetStoredFilePath(setting, fileKey);
        if (string.IsNullOrWhiteSpace(storedPath) || !_settingsFileStorage.IsManagedFile(storedPath) || !System.IO.File.Exists(storedPath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(storedPath, out var mimeType))
            mimeType = "application/octet-stream";

        return PhysicalFile(storedPath, mimeType, enableRangeProcessing: false);
    }

    private ToolRentalDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
        optionsBuilder.UseSqlServer(
            _runtimeSqlSettingsStore.BuildConnectionString(),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null));

        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        return new ToolRentalDbContext(optionsBuilder.Options);
    }

    private async Task UpdateStoredFileAsync(Setting setting, string fileKey, IFormFile? file, bool clearExisting)
    {
        var currentPath = GetStoredFilePath(setting, fileKey);
        if (clearExisting)
        {
            _settingsFileStorage.TryDeleteManagedFile(currentPath);
            SetStoredFilePath(setting, fileKey, null);
        }

        if (file == null || file.Length <= 0)
            return;

        var savedPath = await _settingsFileStorage.SaveAsync(file, fileKey, GetAllowedExtensions(fileKey));
        _settingsFileStorage.TryDeleteManagedFile(currentPath);
        SetStoredFilePath(setting, fileKey, savedPath);
    }

    private object DescribeStoredFile(string fileKey, string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return new
            {
                key = fileKey,
                fileName = string.Empty,
                storedPath = string.Empty,
                exists = false,
                canServe = false,
                downloadUrl = (string?)null,
                previewUrl = (string?)null,
                status = "Nincs fájl feltöltve."
            };
        }

        var exists = System.IO.File.Exists(storedPath);
        var canServe = exists && _settingsFileStorage.IsManagedFile(storedPath);
        var url = canServe ? $"/api/settings/files/{fileKey}" : null;

        return new
        {
            key = fileKey,
            fileName = Path.GetFileName(storedPath),
            storedPath,
            exists,
            canServe,
            downloadUrl = url,
            previewUrl = fileKey == "companyLogo" ? url : null,
            status = exists
                ? (canServe ? "Fájl elérhető a szerveren." : "A fájl megvan, de nem webes feltöltésből származik.")
                : "A korábban mentett fájl ezen a szerveren nem található."
        };
    }

    private static string? ValidateSqlSettings(SqlConnectionSettings settings)
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

    private static int ParseIntOrDefault(string? value, int fallback, int minimum)
    {
        return int.TryParse(value, out var parsed) && parsed >= minimum ? parsed : fallback;
    }

    private static bool ParseBool(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string[] GetAllowedExtensions(string fileKey)
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

    private static string? GetStoredFilePath(Setting setting, string fileKey)
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

    private static void SetStoredFilePath(Setting setting, string fileKey, string? value)
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
}
