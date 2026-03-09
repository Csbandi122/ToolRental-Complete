using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class Rental
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string TicketNr { get; set; } = string.Empty;

        public int CustomerId { get; set; }
        public DateTime RentStart { get; set; }

        [Range(1, 365)]
        public int RentalDays { get; set; }

        [Required, StringLength(50)]
        public string PaymentMode { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Comment { get; set; }

        [StringLength(500)]
        public string? Contract { get; set; }

        [StringLength(500)]
        public string? Invoice { get; set; }

        public bool ReviewEmailSent { get; set; } = false;

        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        public bool ContractEmailSent { get; set; } = false;
        public bool InvoiceEmailSent { get; set; } = false;

        // Navigation Properties
        public Customer Customer { get; set; } = null!;
        public ICollection<RentalDevice> RentalDevices { get; set; } = new List<RentalDevice>();
    }
}
