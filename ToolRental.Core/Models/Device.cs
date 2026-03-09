using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class Device
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string DeviceName { get; set; } = string.Empty;

        public int DeviceType { get; set; }

        [StringLength(100)]
        public string Serial { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Range(0, double.MaxValue)]
        public decimal RentPrice { get; set; }

        public bool Available { get; set; } = true;

        [StringLength(500)]
        public string? Picture { get; set; }

        [Range(0, int.MaxValue)]
        public int RentCount { get; set; } = 0;

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Navigation Properties
        public DeviceType? DeviceTypeNavigation { get; set; }
        public ICollection<RentalDevice> RentalDevices { get; set; } = new List<RentalDevice>();
        public ICollection<FinancialDevice> FinancialDevices { get; set; } = new List<FinancialDevice>();
        public ICollection<ServiceDevice> ServiceDevices { get; set; } = new List<ServiceDevice>();
    }
}
