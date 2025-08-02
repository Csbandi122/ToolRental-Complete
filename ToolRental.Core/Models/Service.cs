namespace ToolRental.Core.Models
{
    public class Service
    {
        public int Id { get; set; }
        public string TicketNr { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Technician { get; set; } = "András";
        public DateTime ServiceDate { get; set; }
        public decimal CostAmount { get; set; }
    }
}