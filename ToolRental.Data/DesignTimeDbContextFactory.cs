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
            // Migrációhoz használható fallback – csak fejlesztési környezetben!
            // Migráció futtatáskor megadható argumentumként:
            // dotnet ef migrations add MigrationName -- "Server=...;Database=...;"
            if (args != null && args.Length > 0 && args[0].StartsWith("Server="))
                return args[0];

            // Utolsó fallback: localhost alapértelmezett kapcsolat
            return "Server=localhost,1433;Database=ToolRentalDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;";
        }
    }
}
