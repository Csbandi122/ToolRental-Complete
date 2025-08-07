namespace ToolRental.Core.Models
{
    public class ServiceDevice
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public int DeviceId { get; set; }

        // Navigation Properties
        public Service Service { get; set; } = null!;
        public Device Device { get; set; } = null!;
    }
}