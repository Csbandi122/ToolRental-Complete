namespace ToolRental.Core.Models
{
    public class RentalDevice
    {
        public int Id { get; set; }
        public int RentalId { get; set; }
        public int DeviceId { get; set; }

        // Navigation Properties
        public Rental Rental { get; set; } = null!;
        public Device Device { get; set; } = null!;
    }
}