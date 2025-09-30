namespace ToolRental.WebAPI.ApiService.Dtos
{
    public class CustomerDto
    {
        public string Name { get; set; }
    }

    public class DeviceDto
    {
        public string DeviceName { get; set; }
    }

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