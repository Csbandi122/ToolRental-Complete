using Serilog;
using Serilog.Events;
using System.IO;

namespace berles2
{
    /// <summary>
    /// Központi naplózó - az alkalmazás bármely pontjáról elérhető statikusan.
    /// Naplófájl helye: %LocalAppData%\ToolRental\logs\berles2-.log (naponta rotál)
    /// </summary>
    internal static class AppLogger
    {
        public static ILogger Logger { get; private set; } = Serilog.Core.Logger.None;

        public static void Initialize()
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ToolRental", "logs");

            Directory.CreateDirectory(logDirectory);

            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "berles2-.log"),
                    rollingInterval: RollingInterval.Day,        // naponta új fájl
                    retainedFileCountLimit: 30,                  // 30 napig tárolja
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug()                                 // VS Output ablakba is ír
                .CreateLogger();

            Logger.Information("=== ToolRental alkalmazás elindult ===");
        }

        public static void CloseAndFlush()
        {
            Logger.Information("=== ToolRental alkalmazás leállt ===");
            (Logger as IDisposable)?.Dispose();
        }
    }
}
