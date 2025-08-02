using System.Configuration;
using System.Data;
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
            // Adatbázis és tesztadatok inicializálása
            using (var context = new ToolRentalDbContext(
                new DbContextOptionsBuilder<ToolRentalDbContext>()
                    .UseSqlite("Data Source=ToolRental.db")
                    .Options))
            {
                SeedData.Initialize(context);
            }

            base.OnStartup(e);
        }
    }
}