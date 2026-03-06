using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ToolRental.Data
{
    /// <summary>
    /// Design-time DbContext factory az EF Core migrációkhoz.
    /// A kapcsolati adatokat az appsettings.json fájlból olvassa,
    /// ha az nem elérhető, fallback connection string-et használ.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ToolRentalDbContext>
    {
        public ToolRentalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();

            // Megpróbáljuk beolvasni a connection string-et az appsettings.json-ból
            string connectionString = TryReadFromAppSettings() ?? GetFallbackConnectionString(args);

            optionsBuilder.UseSqlServer(connectionString);
            return new ToolRentalDbContext(optionsBuilder.Options);
        }

        private static string? TryReadFromAppSettings()
        {
            try
            {
                // Keressük az appsettings.json-t a berles2 projekt közelében
                string[] searchPaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "berles2", "appsettings.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "berles2", "appsettings.json"),
                };

                foreach (var path in searchPaths)
                {
                    if (!File.Exists(path)) continue;

                    var config = new ConfigurationBuilder()
                        .AddJsonFile(path, optional: false, reloadOnChange: false)
                        .Build();

                    var server = config["DatabaseSettings:Server"];
                    var port = config["DatabaseSettings:Port"] ?? "1433";
                    var database = config["DatabaseSettings:Database"];
                    var userId = config["DatabaseSettings:UserId"];
                    var password = config["DatabaseSettings:Password"];
                    var trustCert = config["DatabaseSettings:TrustServerCertificate"] ?? "true";

                    if (!string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(database)
                        && !string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(password))
                    {
                        return $"Server={server},{port};Database={database};User Id={userId};Password={password};TrustServerCertificate={trustCert};";
                    }
                }
            }
            catch
            {
                // Ha nem sikerül olvasni, a fallback-et használjuk
            }

            return null;
        }

        private static string GetFallbackConnectionString(string[] args)
        {
            // 1. Migráció CLI argumentumból: dotnet ef migrations add Name -- "Server=...;"
            if (args != null && args.Length > 0 && args[0].StartsWith("Server="))
                return args[0];

            // 2. Environment változóból (CI/CD pipeline vagy lokális fejlesztés)
            // Beállítás: setx TOOLRENTAL_CONNECTION_STRING "Server=...;Database=...;..."
            var envConnStr = Environment.GetEnvironmentVariable("TOOLRENTAL_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(envConnStr))
                return envConnStr;

            throw new InvalidOperationException(
                "EF Core migráció futtatásához nincs connection string megadva.\n" +
                "Lehetőségek:\n" +
                "  1. CLI arg:    dotnet ef migrations add Nev -- \"Server=...;Database=...;\"\n" +
                "  2. Env változó: TOOLRENTAL_CONNECTION_STRING=\"Server=...;Database=...;\"\n" +
                "  3. appsettings.json a berles2 mappában kitöltve");
        }
    }
}
