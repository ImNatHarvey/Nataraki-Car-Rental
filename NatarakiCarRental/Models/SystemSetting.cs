namespace NatarakiCarRental.Models;

public sealed class SystemSetting
{
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
