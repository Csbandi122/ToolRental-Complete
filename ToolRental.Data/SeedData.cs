using ToolRental.Core.Models;

namespace ToolRental.Data
{
    public static class SeedData
    {
        public static void Initialize(ToolRentalDbContext context)
        {
            // Adatbázis létrehozása ha nem létezik
            context.Database.EnsureCreated();

            // Üres adatbázist hagyunk, nincs teszt adat
        }
    }
}