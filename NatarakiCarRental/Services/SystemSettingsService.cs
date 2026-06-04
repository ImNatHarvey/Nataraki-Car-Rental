using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class SystemSettingsService
{
    private readonly SystemSettingsRepository _repository;
    private readonly ActivityLogService _activityLogService;
    private readonly NotificationService _notificationService = new();

    public SystemSettingsService() : this(new SystemSettingsRepository(), new ActivityLogService(), new NotificationService())
    {
    }

    public SystemSettingsService(SystemSettingsRepository repository, ActivityLogService activityLogService, NotificationService notificationService)
    {
        _repository = repository;
        _activityLogService = activityLogService;
        _notificationService = notificationService;
    }

    public async Task<SystemSettingsModel> GetSettingsAsync()
    {
        var settingsList = await _repository.GetAllAsync();
        var model = new SystemSettingsModel();
        
        foreach (var setting in settingsList)
        {
            switch (setting.SettingKey)
            {
                case "BusinessName": model.BusinessName = setting.SettingValue ?? "Nataraki Car Rental"; break;
                case "ContactNumber": model.ContactNumber = setting.SettingValue ?? ""; break;
                case "EmailAddress": model.EmailAddress = setting.SettingValue ?? ""; break;
                case "BusinessAddress": model.BusinessAddress = setting.SettingValue ?? ""; break;
                case "BusinessRegionCode": model.BusinessRegionCode = setting.SettingValue ?? ""; break;
                case "BusinessRegionName": model.BusinessRegionName = setting.SettingValue ?? ""; break;
                case "BusinessProvinceCode": model.BusinessProvinceCode = setting.SettingValue ?? ""; break;
                case "BusinessProvinceName": model.BusinessProvinceName = setting.SettingValue ?? ""; break;
                case "BusinessCityCode": model.BusinessCityCode = setting.SettingValue ?? ""; break;
                case "BusinessCityName": model.BusinessCityName = setting.SettingValue ?? ""; break;
                case "BusinessBarangayCode": model.BusinessBarangayCode = setting.SettingValue ?? ""; break;
                case "BusinessBarangayName": model.BusinessBarangayName = setting.SettingValue ?? ""; break;
                case "BusinessStreetAddress": model.BusinessStreetAddress = setting.SettingValue ?? ""; break;
                case "ThemeColor": model.ThemeColor = setting.SettingValue ?? "#2563EB"; break;
                case "SystemIconPath": model.SystemIconPath = setting.SettingValue ?? ""; break;
                case "SystemLogoMode": model.SystemLogoMode = setting.SettingValue ?? "BuiltIn"; break;
                case "SystemLogoIconKey": model.SystemLogoIconKey = setting.SettingValue ?? "Car"; break;
                case "LoginPosterPath": model.LoginPosterPath = setting.SettingValue ?? ""; break;
                case "UseCustomLoginPoster": model.UseCustomLoginPoster = bool.TryParse(setting.SettingValue, out bool result) && result; break;
                case "LoginDescription": model.LoginDescription = setting.SettingValue ?? "Internal scheduling and record management system"; break;
                case "ReportHeaderName": model.ReportHeaderName = setting.SettingValue ?? "Nataraki Car Rental"; break;
            }
        }
        
        return model;
    }

    public async Task SaveSystemSettingsAsync(SystemSettingsModel model, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Settings");
        
        var oldSettings = await GetSettingsAsync();
        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldSettings, model);

        if (oldValue == null) return; // Only log and update if ACTUAL changes occurred

        var dict = new Dictionary<string, string?>
        {
            { "BusinessName", model.BusinessName.Trim() },
            { "ContactNumber", model.ContactNumber.Trim() },
            { "EmailAddress", model.EmailAddress.Trim() },
            { "BusinessAddress", model.BusinessAddress.Trim() },
            { "BusinessRegionCode", model.BusinessRegionCode },
            { "BusinessRegionName", model.BusinessRegionName },
            { "BusinessProvinceCode", model.BusinessProvinceCode },
            { "BusinessProvinceName", model.BusinessProvinceName },
            { "BusinessCityCode", model.BusinessCityCode },
            { "BusinessCityName", model.BusinessCityName },
            { "BusinessBarangayCode", model.BusinessBarangayCode },
            { "BusinessBarangayName", model.BusinessBarangayName },
            { "BusinessStreetAddress", model.BusinessStreetAddress.Trim() },
            { "LoginDescription", model.LoginDescription.Trim() },
            { "ReportHeaderName", model.BusinessName.Trim() } // Sync report header name with business name for now
        };

        await _repository.SetManyAsync(dict, currentUserId);
        
        await _activityLogService.LogAsync(
            "Updated",
            "SystemSettings",
            entityId: null,
            description: "Updated system settings.",
            userId: currentUserId,
            entityName: "System Settings",
            oldValue: oldValue,
            newValue: newValue);

        await _notificationService.NotifyAsync(
            "Settings Updated",
            "System configuration settings have been updated.",
            type: "Info",
            module: "SystemSettings");
    }

    public async Task SaveBrandingSettingsAsync(SystemSettingsModel model, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Branding");

        var oldSettings = await GetSettingsAsync();
        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldSettings, model);

        if (oldValue == null) return; // Only log and update if ACTUAL changes occurred

        var dict = new Dictionary<string, string?>
        {
            { "ThemeColor", model.ThemeColor },
            { "SystemIconPath", model.SystemIconPath },
            { "SystemLogoMode", model.SystemLogoMode },
            { "SystemLogoIconKey", model.SystemLogoIconKey },
            { "LoginPosterPath", model.LoginPosterPath },
            { "UseCustomLoginPoster", model.UseCustomLoginPoster.ToString().ToLower() },
            { "LoginDescription", model.LoginDescription.Trim() }
        };

        await _repository.SetManyAsync(dict, currentUserId);

        await _activityLogService.LogAsync(
            "Updated",
            "SystemSettings",
            entityId: null,
            description: "Updated branding and theme settings.",
            userId: currentUserId,
            entityName: "Branding Settings",
            oldValue: oldValue,
            newValue: newValue);

        await _notificationService.NotifyAsync(
            "Branding Updated",
            "System branding and theme settings have been updated.",
            type: "Info",
            module: "SystemSettings");
    }

    public async Task ResetDefaultsAsync(int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Settings");

        var oldSettings = await GetSettingsAsync();
        var defaultModel = new SystemSettingsModel();
        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldSettings, defaultModel);

        var dict = new Dictionary<string, string?>
        {
            { "BusinessName", "Nataraki Car Rental" },
            { "ContactNumber", "" },
            { "EmailAddress", "" },
            { "BusinessAddress", "" },
            { "BusinessRegionCode", "" },
            { "BusinessRegionName", "" },
            { "BusinessProvinceCode", "" },
            { "BusinessProvinceName", "" },
            { "BusinessCityCode", "" },
            { "BusinessCityName", "" },
            { "BusinessBarangayCode", "" },
            { "BusinessBarangayName", "" },
            { "BusinessStreetAddress", "" },
            { "ThemeColor", "#2563EB" },
            { "SystemIconPath", "" },
            { "SystemLogoMode", "BuiltIn" },
            { "SystemLogoIconKey", "Car" },
            { "LoginPosterPath", "" },
            { "UseCustomLoginPoster", "false" },
            { "LoginDescription", "Internal scheduling and record management system" },
            { "ReportHeaderName", "Nataraki Car Rental" }
        };

        await _repository.SetManyAsync(dict, currentUserId);

        await _activityLogService.LogAsync(
            "Updated",
            "SystemSettings",
            entityId: null,
            description: "Reset system settings to defaults.",
            userId: currentUserId,
            entityName: "System Settings",
            oldValue: oldValue,
            newValue: newValue);

        await _notificationService.NotifyAsync(
            "Settings Reset",
            "System settings have been reset to their default values.",
            type: "Warning",
            module: "SystemSettings");
    }
}
