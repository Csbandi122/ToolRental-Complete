namespace ToolRental.Core.Models
{
    public class ServicePart
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public int PartId { get; set; }
        public int Quantity { get; set; } = 1;

        // Navigation Properties
        public Service Service { get; set; } = null!;
        public Part Part { get; set; } = null!;
    }
}
