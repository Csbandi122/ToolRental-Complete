namespace ToolRental.Core.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int DeviceType { get; set; }
        public string Serial { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal RentPrice { get; set; }
        public bool Available { get; set; } = true;
        public string? Picture { get; set; }
        public int RentCount { get; set; } = 0;
        public string? Notes { get; set; }

        // Navigation Properties
        public DeviceType? DeviceTypeNavigation { get; set; }
        public ICollection<RentalDevice> RentalDevices { get; set; } = new List<RentalDevice>();
        // Device.cs Navigation Properties részébe add hozzá:
        public ICollection<FinancialDevice> FinancialDevices { get; set; } = new List<FinancialDevice>();
    }
}