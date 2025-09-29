namespace ToolRental.WebAPI.ApiService.Dtos
{
    // Egyszerűsített ügyfél, csak a név kell
    public class CustomerDto
    {
        public string Name { get; set; }
    }

    // Egyszerűsített eszköz, csak a név kell
    public class DeviceDto
    {
        public string DeviceName { get; set; }
    }

    // A fő DTO a bérléshez - HELYES VERZIÓ
    public class RentalDto
    {
        public int Id { get; set; }
        public string TicketNr { get; set; }
        public DateTime RentStart { get; set; }
        public int RentalDays { get; set; }
        public decimal TotalAmount { get; set; }

        public CustomerDto Customer { get; set; }
        public List<DeviceDto> Devices { get; set; } = new List<DeviceDto>();
    }
}