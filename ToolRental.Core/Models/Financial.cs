namespace ToolRental.Core.Models
{
    public class Financial
    {
        public int Id { get; set; }
        public string TicketNr { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty; // "bevétel", "költség"
        public string SourceType { get; set; } = string.Empty; // "bérlés", "szervíz", "eszköz_vásárlás", "marketing", "egyéb"
        public int? SourceId { get; set; }
        public DateTime Date { get; set; }
        public string? Comment { get; set; }
        public decimal Amount { get; set; }

        // Navigation Properties
        public ICollection<FinancialDevice> FinancialDevices { get; set; } = new List<FinancialDevice>();
    }
}