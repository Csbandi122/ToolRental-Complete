namespace ToolRental.Core.Models
{
    public class Rental
    {
        public int Id { get; set; }
        public string TicketNr { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public DateTime RentStart { get; set; }
        public int RentalDays { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public string? Contract { get; set; }
        public string? Invoice { get; set; }
        public bool ReviewEmailSent { get; set; } = false;
        public decimal TotalAmount { get; set; }
        public bool ContractEmailSent { get; set; } = false;
        public bool InvoiceEmailSent { get; set; } = false;

        // Navigation Properties
        public Customer Customer { get; set; } = null!;
        public ICollection<RentalDevice> RentalDevices { get; set; } = new List<RentalDevice>();
    }
}