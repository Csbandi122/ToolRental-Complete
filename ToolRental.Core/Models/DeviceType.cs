using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class DeviceType
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string TypeName { get; set; } = string.Empty;

        // Navigation Properties
        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}
