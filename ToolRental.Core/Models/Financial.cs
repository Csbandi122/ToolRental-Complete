using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class Financial
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string TicketNr { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string EntryType { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string SourceType { get; set; } = string.Empty;

        public int? SourceId { get; set; }
        public DateTime Date { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public decimal Amount { get; set; }

        // Navigation Properties
        public ICollection<FinancialDevice> FinancialDevices { get; set; } = new List<FinancialDevice>();
    }
}
