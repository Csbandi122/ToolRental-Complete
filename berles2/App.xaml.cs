using System.Windows;
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

            // Ha még nincs beállítva az SQL szerver kapcsolat, megnyitjuk a beállítások ablakot
            if (!DatabaseConfig.IsConfigured)
            {
                var settingsDialog = new SettingsDialog();
                bool? result = settingsDialog.ShowDialog();

                // Ha a felhasználó bezárta mentés nélkül, leállítjuk az appot
                if (result != true || !DatabaseConfig.IsConfigured)
                {
                    MessageBox.Show(
                        "Az SQL szerver kapcsolat beállítása kötelező az alkalmazás indításához.\n" +
                        "Az alkalmazás bezárul.",
                        "Hiányzó beállítás",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }
            }

            // Adatbázis létrehozása, ha még nem létezik
            try
            {
                using var context = new ToolRentalDbContext(
                    new DbContextOptionsBuilder<ToolRentalDbContext>()
                        .UseSqlServer(DatabaseConfig.ConnectionString)
                        .Options);
                context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Nem sikerült csatlakozni az adatbázishoz:\n{ex.Message}\n\n" +
                    "Ellenőrizd az SQL szerver beállításokat a Beállítások menüben.",
                    "Adatbázis hiba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                // Folytatjuk az indítást – a főablak is megmutatja a hibát
            }
        }
    }
}
