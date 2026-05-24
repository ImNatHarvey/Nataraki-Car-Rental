namespace NatarakiCarRental.Models;

public sealed class SystemSettingsModel
{
    public string BusinessName { get; set; } = "Nataraki Car Rental";
    public string ContactNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string BusinessRegionCode { get; set; } = string.Empty;
    public string BusinessRegionName { get; set; } = string.Empty;
    public string BusinessProvinceCode { get; set; } = string.Empty;
    public string BusinessProvinceName { get; set; } = string.Empty;
    public string BusinessCityCode { get; set; } = string.Empty;
    public string BusinessCityName { get; set; } = string.Empty;
    public string BusinessBarangayCode { get; set; } = string.Empty;
    public string BusinessBarangayName { get; set; } = string.Empty;
    public string BusinessStreetAddress { get; set; } = string.Empty;
    public string ThemeColor { get; set; } = "#2563EB";
    public string SystemIconPath { get; set; } = string.Empty;
    public string SystemLogoMode { get; set; } = "BuiltIn";
    public string SystemLogoIconKey { get; set; } = "Car";
    public string LoginPosterPath { get; set; } = string.Empty;
    public bool UseCustomLoginPoster { get; set; } = false;
    public string LoginDescription { get; set; } = "Internal scheduling and record management system";
    public string ReportHeaderName { get; set; } = "Nataraki Car Rental";
}
