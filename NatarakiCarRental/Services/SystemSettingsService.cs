using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class SystemSettingsService
{
    private readonly SystemSettingsRepository _repository;
    private readonly ActivityLogService _activityLogService;

    public SystemSettingsService() : this(new SystemSettingsRepository(), new ActivityLogService())
    {
    }

    public SystemSettingsService(SystemSettingsRepository repository, ActivityLogService activityLogService)
    {
        _repository = repository;
        _activityLogService = activityLogService;
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
                case "ThemeColor": model.ThemeColor = setting.SettingValue ?? "#2563EB"; break;
                case "SystemIconPath": model.SystemIconPath = setting.SettingValue ?? ""; break;
                case "LoginPosterPath": model.LoginPosterPath = setting.SettingValue ?? ""; break;
                case "UseCustomLoginPoster": model.UseCustomLoginPoster = bool.TryParse(setting.SettingValue, out bool result) && result; break;
                case "ReportHeaderName": model.ReportHeaderName = setting.SettingValue ?? "Nataraki Car Rental"; break;
            }
        }
        
        return model;
    }

    public async Task SaveSystemSettingsAsync(SystemSettingsModel model, int currentUserId)
    {
        var dict = new Dictionary<string, string?>
        {
            { "BusinessName", model.BusinessName.Trim() },
            { "ContactNumber", model.ContactNumber.Trim() },
            { "EmailAddress", model.EmailAddress.Trim() },
            { "BusinessAddress", model.BusinessAddress.Trim() },
            { "ReportHeaderName", model.BusinessName.Trim() } // Sync report header name with business name for now
        };

        await _repository.SetManyAsync(dict, currentUserId);
        
        await _activityLogService.LogAsync(
            "Update",
            "SystemSettings",
            null,
            "Updated system settings.",
            currentUserId);
    }

    public async Task SaveBrandingSettingsAsync(SystemSettingsModel model, int currentUserId)
    {
        var dict = new Dictionary<string, string?>
        {
            { "ThemeColor", model.ThemeColor },
            { "SystemIconPath", model.SystemIconPath },
            { "LoginPosterPath", model.LoginPosterPath },
            { "UseCustomLoginPoster", model.UseCustomLoginPoster.ToString().ToLower() }
        };

        await _repository.SetManyAsync(dict, currentUserId);

        await _activityLogService.LogAsync(
            "Update",
            "SystemSettings",
            null,
            "Updated branding and theme settings.",
            currentUserId);
    }

    public async Task ResetDefaultsAsync(int currentUserId)
    {
        var dict = new Dictionary<string, string?>
        {
            { "BusinessName", "Nataraki Car Rental" },
            { "ContactNumber", "" },
            { "EmailAddress", "" },
            { "BusinessAddress", "" },
            { "ThemeColor", "#2563EB" },
            { "SystemIconPath", "" },
            { "LoginPosterPath", "" },
            { "UseCustomLoginPoster", "false" },
            { "ReportHeaderName", "Nataraki Car Rental" }
        };

        await _repository.SetManyAsync(dict, currentUserId);

        await _activityLogService.LogAsync(
            "System",
            "SystemSettings",
            null,
            "Reset system settings to defaults.",
            currentUserId);
    }
}
