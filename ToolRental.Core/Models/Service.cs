using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class Service
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string TicketNr { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string ServiceType { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string Technician { get; set; } = string.Empty;

        public DateTime ServiceDate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CostAmount { get; set; }

        // Navigation Properties
        public ICollection<ServiceDevice> ServiceDevices { get; set; } = new List<ServiceDevice>();
    }
}
