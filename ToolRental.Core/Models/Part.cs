using System.ComponentModel.DataAnnotations;

namespace ToolRental.Core.Models
{
    public class Part
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        // Navigation Properties
        public ICollection<ServicePart> ServiceParts { get; set; } = new List<ServicePart>();
    }
}
