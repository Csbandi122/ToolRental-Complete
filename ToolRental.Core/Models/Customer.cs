using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(10)]
        public string Zipcode { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(300)]
        public string Address { get; set; } = string.Empty;

        [Required, StringLength(200), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        public string IdNumber { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Comment { get; set; }

        // Navigation Properties
        public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    }
}
