using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using ToolRental.Data;

namespace berles2
{
    public static class DatabaseConfig
    {
        private static IConfiguration? _configuration;

        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    _configuration = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                        .Build();
                }
                return _configuration;
            }
        }

        public static string Server => Configuration["DatabaseSettings:Server"] ?? "";
        public static int Port => int.TryParse(Configuration["DatabaseSettings:Port"], out int p) ? p : 1433;
        public static string Database => Configuration["DatabaseSettings:Database"] ?? "";
        public static string UserId => Configuration["DatabaseSettings:UserId"] ?? "";

        /// <summary>
        /// A jelszót visszafejti, ha titkosítva van tárolva az appsettings.json-ban.
        /// </summary>
        public static string Password => CredentialProtection.Unprotect(Configuration["DatabaseSettings:Password"] ?? "");
        public static bool TrustServerCertificate => bool.TryParse(Configuration["DatabaseSettings:TrustServerCertificate"], out bool t) ? t : true;

        /// <summary>
        /// Visszaadja a connection string-et az appsettings.json alapján.
        /// Ha nincs beállítva, üres string-et ad vissza.
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database)
                    || string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Password))
                {
                    return string.Empty;
                }

                return $"Server={Server},{Port};Database={Database};User Id={UserId};Password={Password};TrustServerCertificate={TrustServerCertificate};";
            }
        }

        /// <summary>
        /// Ellenőrzi, hogy be vannak-e állítva az SQL szerver adatok.
        /// </summary>
        public static bool IsConfigured => !string.IsNullOrEmpty(ConnectionString);

        /// <summary>
        /// Igaz, ha a TEST adatbázisra csatlakozunk (ToolRentalDB_TEST).
        /// </summary>
        public static bool IsTestMode => Database == "ToolRentalDB_TEST";

        /// <summary>
        /// Átváltja az adatbázist PROD ↔ TEST között.
        /// Minden más beállítás (szerver, port, user, jelszó) változatlan marad.
        /// </summary>
        public static void SwitchDatabase(string newDatabaseName)
        {
            Save(Server, Port, newDatabaseName, UserId, Password, TrustServerCertificate);
        }

        /// <summary>
        /// DbContext opciókat ad vissza automatikus újrapróbálkozással (max 3x, 5 mp késleltetéssel).
        /// Átmeneti hálózati hibák esetén az alkalmazás nem fagy be, hanem újrapróbál.
        /// </summary>
        public static DbContextOptions<ToolRentalDbContext> GetOptions()
        {
            return new DbContextOptionsBuilder<ToolRentalDbContext>()
                .UseSqlServer(ConnectionString, sqlOptions =>
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null))
                .Options;
        }

        /// <summary>
        /// Elmenti az SQL szerver beállításokat az appsettings.json fájlba.
        /// </summary>
        public static void Save(string server, int port, string database, string userId, string password, bool trustServerCertificate)
        {
            var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            // A jelszót titkosítva tároljuk – csak ezen a gépen fejthető vissza (Windows DPAPI)
            var protectedPassword = CredentialProtection.Protect(password);

            var json = $@"{{
  ""DatabaseSettings"": {{
    ""Server"": ""{EscapeJson(server)}"",
    ""Port"": {port},
    ""Database"": ""{EscapeJson(database)}"",
    ""UserId"": ""{EscapeJson(userId)}"",
    ""Password"": ""{EscapeJson(protectedPassword)}"",
    ""TrustServerCertificate"": {trustServerCertificate.ToString().ToLower()}
  }}
}}";
            File.WriteAllText(appSettingsPath, json);

            // Konfiguráció újratöltése
            _configuration = null;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
