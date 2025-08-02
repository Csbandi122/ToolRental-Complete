namespace ToolRental.Core.Models
{
    public class DeviceType
    {
        public int Id { get; set; }
        public string TypeName { get; set; } = string.Empty;

        // Navigation Properties
        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}