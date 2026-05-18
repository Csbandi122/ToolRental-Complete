using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace ToolRental.Bikes;

public sealed class RuntimeSqlSettingsStore
{
    private const string ProtectedPrefix = "WEBENC:";

    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private SqlConnectionSettings? _cachedSettings;
    private readonly SqlConnectionSettings _fallbackSettings;

    public RuntimeSqlSettingsStore(IWebHostEnvironment environment, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
    {
        var appDataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataDirectory);

        _filePath = Path.Combine(appDataDirectory, "runtime-sql-settings.json");
        _protector = dataProtectionProvider.CreateProtector("ToolRental.Bikes.RuntimeSqlSettings.v1");
        _fallbackSettings = BuildFallbackSettings(configuration);
    }

    public SqlConnectionSettings GetSettings()
    {
        lock (_sync)
        {
            _cachedSettings ??= LoadSettingsCore();
            return _cachedSettings.Clone();
        }
    }

    public void SaveSettings(SqlConnectionSettings settings)
    {
        var normalized = settings.Clone();

        lock (_sync)
        {
            var stored = new StoredSqlConnectionSettings
            {
                Server = normalized.Server,
                Port = normalized.Port,
                Database = normalized.Database,
                UserId = normalized.UserId,
                ProtectedPassword = Protect(normalized.Password),
                TrustServerCertificate = normalized.TrustServerCertificate,
                TestDatabaseName = normalized.TestDatabaseName
            };

            var json = JsonSerializer.Serialize(stored, _jsonOptions);
            File.WriteAllText(_filePath, json);
            _cachedSettings = normalized;
        }
    }

    public string BuildConnectionString(string mode)
    {
        var settings = GetSettings();
        var databaseName = string.Equals(mode, "test", StringComparison.OrdinalIgnoreCase)
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

    private SqlConnectionSettings LoadSettingsCore()
    {
        if (!File.Exists(_filePath))
            return _fallbackSettings.Clone();

        try
        {
            var json = File.ReadAllText(_filePath);
            var stored = JsonSerializer.Deserialize<StoredSqlConnectionSettings>(json, _jsonOptions);
            if (stored == null)
                return _fallbackSettings.Clone();

            return new SqlConnectionSettings
            {
                Server = stored.Server ?? _fallbackSettings.Server,
                Port = stored.Port > 0 ? stored.Port : _fallbackSettings.Port,
                Database = stored.Database ?? _fallbackSettings.Database,
                UserId = stored.UserId ?? _fallbackSettings.UserId,
                Password = Unprotect(stored.ProtectedPassword),
                TrustServerCertificate = stored.TrustServerCertificate,
                TestDatabaseName = string.IsNullOrWhiteSpace(stored.TestDatabaseName)
                    ? _fallbackSettings.TestDatabaseName
                    : stored.TestDatabaseName
            };
        }
        catch
        {
            return _fallbackSettings.Clone();
        }
    }

    private SqlConnectionSettings BuildFallbackSettings(IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var builder = new SqlConnectionStringBuilder(defaultConnection);
        var (server, port) = ParseDataSource(builder.DataSource);

        return new SqlConnectionSettings
        {
            Server = server,
            Port = port,
            Database = builder.InitialCatalog ?? string.Empty,
            UserId = builder.UserID ?? string.Empty,
            Password = builder.Password ?? string.Empty,
            TrustServerCertificate = builder.TrustServerCertificate,
            TestDatabaseName = configuration["DatabaseSwitching:TestDatabaseName"] ?? "ToolRentalDB_Test"
        };
    }

    private (string Server, int Port) ParseDataSource(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
            return ("localhost", 1433);

        var normalized = dataSource.Trim();
        if (normalized.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];

        var parts = normalized.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out var parsedPort) && parsedPort > 0)
            return (parts[0], parsedPort);

        return (normalized, 1433);
    }

    private string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var protectedValue = _protector.Protect(value);
        return ProtectedPrefix + protectedValue;
    }

    private string Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (!value.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
            return value;

        try
        {
            return _protector.Unprotect(value[ProtectedPrefix.Length..]);
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class StoredSqlConnectionSettings
    {
        public string? Server { get; set; }
        public int Port { get; set; }
        public string? Database { get; set; }
        public string? UserId { get; set; }
        public string? ProtectedPassword { get; set; }
        public bool TrustServerCertificate { get; set; }
        public string? TestDatabaseName { get; set; }
    }
}

public sealed class SqlConnectionSettings
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 1433;
    public string Database { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool TrustServerCertificate { get; set; } = true;
    public string TestDatabaseName { get; set; } = "ToolRentalDB_Test";

    public SqlConnectionSettings Clone()
    {
        return new SqlConnectionSettings
        {
            Server = Server,
            Port = Port,
            Database = Database,
            UserId = UserId,
            Password = Password,
            TrustServerCertificate = TrustServerCertificate,
            TestDatabaseName = TestDatabaseName
        };
    }
}
