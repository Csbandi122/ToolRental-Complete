using Microsoft.EntityFrameworkCore;
using ToolRental.Core.Models;
using ToolRental.Core;
using ToolRental.Data;

namespace berles2.Services
{
    /// <summary>
    /// Bérlés mentési üzleti logika — Customer, Rental, Financial rekordok kezelése.
    /// Nincs UI függősége, önállóan tesztelhető.
    /// </summary>
    internal class RentalService
    {
        private readonly ToolRentalDbContext _context;

        public RentalService(ToolRentalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ===========================================
        // PUBLIKUS BELÉPÉSI PONT
        // ===========================================

        /// <summary>
        /// Egy bérlést ment el tranzakcióban:
        /// Customer (új vagy meglévő) → Rental → RentalDevice-ok → Financial (bevétel) → FinancialDevice-ok.
        /// </summary>
        /// <param name="data">A bérlés összes adata UI-tól függetlenül.</param>
        /// <returns>A létrehozott Rental rekord.</returns>
        public Rental SaveRental(RentalData data)
        {
            // EnableRetryOnFailure esetén CreateExecutionStrategy() szükséges a tranzakcióhoz
            var strategy = _context.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
                using var transaction = _context.Database.BeginTransaction();
                try
                {
                    // 1. Customer kezelése
                    Customer customer = ResolveCustomer(data);

                    // 2. Rental létrehozása
                    var rental = new Rental
                    {
                        TicketNr    = data.TicketNr,
                        CustomerId  = customer.Id,
                        RentStart   = DateTime.Now,
                        RentalDays  = data.RentalDays,
                        PaymentMode = data.PaymentMode,
                        Comment     = data.Comment,
                        TotalAmount = data.TotalAmount
                    };

                    _context.Rentals.Add(rental);
                    _context.SaveChanges();

                    // 3. RentalDevice kapcsolatok + RentCount növelés
                    foreach (var device in data.Devices)
                    {
                        _context.RentalDevices.Add(new RentalDevice
                        {
                            RentalId = rental.Id,
                            DeviceId = device.Id
                        });

                        // A device-t a context-ből kérjük le, hogy a változás mentődjön
                        var trackedDevice = _context.Devices.First(d => d.Id == device.Id);
                        trackedDevice.RentCount++;
                    }

                    // 4. Financial rekord (bevétel)
                    var financial = new Financial
                    {
                        TicketNr   = data.TicketNr,
                        EntryType  = EntryTypes.Bevetel,
                        SourceType = SourceTypes.Berles,
                        SourceId   = rental.Id,
                        Date       = DateTime.Now,
                        Comment    = $"Bérlési díj - {data.TicketNr}",
                        Amount     = data.TotalAmount
                    };

                    _context.Financials.Add(financial);
                    _context.SaveChanges();

                    // 5. FinancialDevice kapcsolatok
                    foreach (var device in data.Devices)
                    {
                        _context.FinancialDevices.Add(new FinancialDevice
                        {
                            FinancialId = financial.Id,
                            DeviceId    = device.Id
                        });
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    AppLogger.Logger.Information(
                        "Bérlés sikeresen mentve: {TicketNr}, ügyfél: {Customer}, összeg: {Amount}",
                        rental.TicketNr, customer.Name, rental.TotalAmount);

                    return rental;
                }
                catch (Exception ex)
                {
                    AppLogger.Logger.Error(ex, "Bérlés mentése sikertelen, tranzakció visszagörgetve");
                    transaction.Rollback();
                    throw;
                }
            });
        }

        // ===========================================
        // BELSŐ SEGÉD METÓDUS
        // ===========================================

        /// <summary>
        /// Ha van kiválasztott meglévő ügyfél, azt adja vissza.
        /// Ha nincs, új Customer rekordot hoz létre és ment.
        /// </summary>
        private Customer ResolveCustomer(RentalData data)
        {
            if (data.ExistingCustomer != null)
                return data.ExistingCustomer;

            var newCustomer = new Customer
            {
                Name     = data.NewCustomerName,
                Zipcode  = data.NewCustomerZip,
                City     = data.NewCustomerCity,
                Address  = data.NewCustomerAddress,
                Email    = data.NewCustomerEmail,
                IdNumber = data.NewCustomerIdNumber,
                Comment  = data.NewCustomerComment
            };

            _context.Customers.Add(newCustomer);
            _context.SaveChanges();
            return newCustomer;
        }
    }

    // ===========================================
    // ADATÁTVITELI OBJEKTUM (DTO)
    // ===========================================

    /// <summary>
    /// A bérlés mentéséhez szükséges összes adat — nincs UI függőség.
    /// </summary>
    internal class RentalData
    {
        // Bérlés azonosítója
        public string TicketNr    { get; init; } = "";
        public int    RentalDays  { get; init; } = 1;
        public string PaymentMode { get; init; } = "Készpénz";
        public string Comment     { get; init; } = "";
        public decimal TotalAmount { get; init; } = 0;

        // Eszközök
        public List<Device> Devices { get; init; } = new();

        // Meglévő ügyfél (ha ki lett választva a listából)
        public Customer? ExistingCustomer { get; init; } = null;

        // Új ügyfél adatai (ha nem meglévőt választottak)
        public string NewCustomerName     { get; init; } = "";
        public string NewCustomerZip      { get; init; } = "";
        public string NewCustomerCity     { get; init; } = "";
        public string NewCustomerAddress  { get; init; } = "";
        public string NewCustomerEmail    { get; init; } = "";
        public string NewCustomerIdNumber { get; init; } = "";
        public string NewCustomerComment  { get; init; } = "";
    }
}
