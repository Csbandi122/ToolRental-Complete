namespace ToolRental.WebApp.DTOs;

public class DeviceDto
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int DeviceType { get; set; }
    public string DeviceTypeName { get; set; } = string.Empty;
    public string Serial { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal RentPrice { get; set; }
    public bool Available { get; set; }
    public string? Picture { get; set; }
    public int RentCount { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReservedUntil { get; set; }
}

public class DeviceCreateUpdateDto
{
    public string DeviceName { get; set; } = string.Empty;
    public int DeviceType { get; set; }
    public string? Serial { get; set; }
    public decimal Price { get; set; }
    public decimal RentPrice { get; set; }
    public bool Available { get; set; } = true;
    public string? Notes { get; set; }
}
