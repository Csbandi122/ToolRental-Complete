namespace ToolRental.Core.Models
{
    public class FinancialDevice  // ← FONTOS: public kell!
    {
        public int Id { get; set; }
        public int FinancialId { get; set; }
        public int DeviceId { get; set; }

        // Navigation Properties
        public Financial Financial { get; set; } = null!;
        public Device Device { get; set; } = null!;
    }
}