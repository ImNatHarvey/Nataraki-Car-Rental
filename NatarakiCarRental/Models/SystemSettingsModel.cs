namespace NatarakiCarRental.Models;

public sealed class SystemSettingsModel
{
    public string BusinessName { get; set; } = "Nataraki Car Rental";
    public string ContactNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string ThemeColor { get; set; } = "#2563EB";
    public string SystemIconPath { get; set; } = string.Empty;
    public string LoginPosterPath { get; set; } = string.Empty;
    public bool UseCustomLoginPoster { get; set; } = false;
    public string ReportHeaderName { get; set; } = "Nataraki Car Rental";
}
