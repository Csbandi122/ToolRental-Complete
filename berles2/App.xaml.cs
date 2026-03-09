using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;

namespace berles2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Naplózás inicializálása – ez legyen az első dolog
            AppLogger.Initialize();

            // Kezeletlen kivételek elkapása – így legalább a napló tartalmazza a crash okát
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            AppLogger.Logger.Information("Alkalmazás konfigurálása...");

            // Ha még nincs beállítva az SQL szerver kapcsolat, megnyitjuk a beállítások ablakot
            if (!DatabaseConfig.IsConfigured)
            {
                AppLogger.Logger.Warning("SQL kapcsolat nincs konfigurálva, beállítások ablak megnyitása");
                var settingsDialog = new SettingsDialog();
                bool? result = settingsDialog.ShowDialog();

                // Ha a felhasználó bezárta mentés nélkül, leállítjuk az appot
                if (result != true || !DatabaseConfig.IsConfigured)
                {
                    AppLogger.Logger.Error("SQL kapcsolat konfigurálása sikertelen, az alkalmazás leáll");
                    MessageBox.Show(
                        "Az SQL szerver kapcsolat beállítása kötelező az alkalmazás indításához.\n" +
                        "Az alkalmazás bezárul.",
                        "Hiányzó beállítás",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    DispatcherUnhandledException -= OnDispatcherUnhandledException;
                    Shutdown();
                    return;
                }
            }

            // Adatbázis létrehozása, ha még nem létezik
            try
            {
                AppLogger.Logger.Information("Adatbázis kapcsolat ellenőrzése: {Server}", DatabaseConfig.Server);
                using var context = new ToolRentalDbContext(DatabaseConfig.GetOptions());
                context.Database.EnsureCreated();
                InitializeSequences(context);
                AppLogger.Logger.Information("Adatbázis kapcsolat sikeres");
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Adatbázis kapcsolat sikertelen");
                MessageBox.Show(
                    $"Nem sikerült csatlakozni az adatbázishoz:\n{ex.Message}\n\n" +
                    "Ellenőrizd az SQL szerver beállításokat a Beállítások menüben.",
                    "Adatbázis hiba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// SEQUENCE objektumok és unique indexek létrehozása, ha még nem léteznek.
        /// A SEQUENCE kezdőértéke a meglévő adatok alapján automatikusan beáll.
        /// </summary>
        private static void InitializeSequences(ToolRentalDbContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = 'RentalTicketSeq')
                    BEGIN
                        DECLARE @maxRnt INT = (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(TicketNr, 4, 10) AS INT)), 0) FROM Rentals WHERE TicketNr LIKE 'RNT%');
                        DECLARE @startRnt INT = @maxRnt + 1;
                        EXEC('CREATE SEQUENCE RentalTicketSeq AS INT START WITH ' + @startRnt + ' INCREMENT BY 1');
                    END

                    IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = 'ServiceTicketSeq')
                    BEGIN
                        DECLARE @maxSrv INT = (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(TicketNr, 4, 10) AS INT)), 0) FROM Services WHERE TicketNr LIKE 'SRV%');
                        DECLARE @startSrv INT = @maxSrv + 1;
                        EXEC('CREATE SEQUENCE ServiceTicketSeq AS INT START WITH ' + @startSrv + ' INCREMENT BY 1');
                    END

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Rentals_TicketNr')
                        CREATE UNIQUE INDEX IX_Rentals_TicketNr ON Rentals(TicketNr);

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Services_TicketNr')
                        CREATE UNIQUE INDEX IX_Services_TicketNr ON Services(TicketNr);
                ");

                AppLogger.Logger.Information("Ticket SEQUENCE-ek és indexek inicializálva");
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Hiba a SEQUENCE inicializálásakor");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger.CloseAndFlush();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Logger.Fatal(e.Exception, "Kezeletlen UI kivétel");
            e.Handled = true;
            MessageBox.Show(
                $"Váratlan hiba történt:\n{e.Exception.Message}\n\nRészletek a naplófájlban.",
                "Váratlan hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLogger.Logger.Fatal(e.ExceptionObject as Exception, "Kezeletlen háttérszál kivétel (crash)");
            AppLogger.CloseAndFlush();
        }
    }
}
