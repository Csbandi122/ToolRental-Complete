namespace ToolRental.WebApp.DTOs;

public class DeviceTypeDto
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int DeviceCount { get; set; }
}

public class DeviceTypeCreateUpdateDto
{
    public string TypeName { get; set; } = string.Empty;
}
