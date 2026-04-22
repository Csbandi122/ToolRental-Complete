namespace ToolRental.Core.Models
{
    public class BikeRelease
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public DateTime ReleaseDate { get; set; }

        public Device? Device { get; set; }
    }
}
