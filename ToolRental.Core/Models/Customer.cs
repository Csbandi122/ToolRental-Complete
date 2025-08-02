namespace ToolRental.Core.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Zipcode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public string? Comment { get; set; }

        // Navigation Properties
        public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    }
}