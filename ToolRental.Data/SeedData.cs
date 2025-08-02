using ToolRental.Core.Models;

namespace ToolRental.Data
{
    public static class SeedData
    {
        public static void Initialize(ToolRentalDbContext context)
        {
            // Adatbázis létrehozása ha nem létezik
            context.Database.EnsureCreated();

            // Ha már vannak adatok, ne csináljunk semmit
            if (context.DeviceTypes.Any())
            {
                return;
            }

            // DeviceType-ok hozzáadása
            var deviceTypes = new DeviceType[]
            {
                new DeviceType { TypeName = "Kerékpár" },
                new DeviceType { TypeName = "Roller" },
                new DeviceType { TypeName = "E-bike" }
            };

            context.DeviceTypes.AddRange(deviceTypes);
            context.SaveChanges();

            // Eszközök hozzáadása
            var devices = new Device[]
            {
                new Device
                {
                    DeviceName = "Trek Mountain Bike",
                    DeviceType = 1, // Kerékpár
                    Serial = "TRK001",
                    Price = 150000,
                    RentPrice = 3000,
                    Available = true,
                    Notes = "Kiváló minőségű hegyi kerékpár"
                },
                new Device
                {
                    DeviceName = "Xiaomi M365 Roller",
                    DeviceType = 2, // Roller
                    Serial = "XIA001",
                    Price = 120000,
                    RentPrice = 2500,
                    Available = true,
                    Notes = "Elektromos roller, 25km hatótáv"
                },
                new Device
                {
                    DeviceName = "Bosch E-bike Urban",
                    DeviceType = 3, // E-bike
                    Serial = "BSH001",
                    Price = 400000,
                    RentPrice = 5000,
                    Available = false,
                    Notes = "Prémium elektromos kerékpár, jelenleg szervízben"
                }
            };

            context.Devices.AddRange(devices);

            // Teszt ügyfél hozzáadása
            var customer = new Customer
            {
                Name = "Teszt János",
                Zipcode = "1051",
                City = "Budapest",
                Address = "Váci utca 123.",
                Email = "teszt.janos@email.com",
                IdNumber = "123456AB",
                Comment = "Rendszeres ügyfél"
            };

            context.Customers.Add(customer);
            context.SaveChanges();
        }
    }
}