using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatarakiCarRental.Helpers;

public static class DiffHelper
{
    public sealed record ChangeEntry(string Field, string From, string To);

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    public static (string? OldValue, string? NewValue) GetJsonDiff<T>(T? oldEntity, T? newEntity) where T : class
    {
        if (oldEntity == null || newEntity == null) return (null, null);

        var oldDict = new Dictionary<string, object?>();
        var newDict = new Dictionary<string, object?>();

        var properties = _propertyCache.GetOrAdd(typeof(T), t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        
        foreach (var prop in properties)
        {
            if (ShouldIgnore(prop.Name)) continue;

            var oldVal = prop.GetValue(oldEntity);
            var newVal = prop.GetValue(newEntity);

            if (!Equals(oldVal, newVal))
            {
                oldDict[prop.Name] = oldVal;
                newDict[prop.Name] = newVal;
            }
        }

        if (oldDict.Count == 0) return (null, null);

        var options = new JsonSerializerOptions { WriteIndented = false };
        return (
            JsonSerializer.Serialize(oldDict, options),
            JsonSerializer.Serialize(newDict, options)
        );
    }

    public static List<ChangeEntry> ParseChanges(string? oldJson, string? newJson)
    {
        if (string.IsNullOrWhiteSpace(oldJson) || string.IsNullOrWhiteSpace(newJson))
            return [];

        try
        {
            var oldDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(oldJson);
            var newDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(newJson);

            if (oldDict == null || newDict == null) return [];

            var changes = new List<ChangeEntry>();
            foreach (var key in oldDict.Keys)
            {
                if (newDict.TryGetValue(key, out var newValue))
                {
                    changes.Add(new ChangeEntry(
                        ToFriendlyName(key),
                        FormatValue(oldDict[key]),
                        FormatValue(newValue)
                    ));
                }
            }

            return changes;
        }
        catch
        {
            return [];
        }
    }

    private static bool ShouldIgnore(string name)
    {
        // Ignore IDs and Timestamps
        return name.EndsWith("Id") || 
               name.EndsWith("At") || 
               name == "PasswordHash" ||
               name == "Salt" ||
               name == "ConcurrencyToken";
    }

    private static string ToFriendlyName(string propertyName)
    {
        // Custom overrides
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FirstName", "First Name" },
            { "LastName", "Last Name" },
            { "PlateNumber", "Plate Number" },
            { "ContactNumber", "Contact Number" },
            { "PhoneNumber", "Phone Number" },
            { "DailyRate", "Daily Rate" },
            { "ProfileImagePath", "Profile Image" },
            { "ValidIdFilePath", "Valid ID" },
            { "ProofOfBillingPath", "Proof of Billing" },
            { "SelfieWithValidIdFilePath", "Selfie with ID" },
            { "DriverLicensePath", "Driver License" },
            { "IsBlacklisted", "Blacklisted" },
            { "BlacklistReason", "Blacklist Reason" },
            { "IsWalkIn", "Walk-In" },
            { "IsArchived", "Archived" },
            { "OffsiteType", "Offsite Type" },
            { "LocationName", "Location" },
            { "ContactPerson", "Contact Person" },
            { "ActualCost", "Actual Cost" },
            { "EstimatedCost", "Estimated Cost" },
            { "TransactionCode", "Transaction Code" },
            { "DailyRate", "Daily Rate" },
            { "TotalAmount", "Total Amount" },
            { "AmountPaid", "Amount Paid" },
            { "BalanceAmount", "Remaining Balance" },
            { "ModeOfPayment", "Payment Mode" },
            { "TransactionStatus", "Transaction Status" },
            { "PaymentStatus", "Payment Status" },
            { "RoleName", "Role Name" },
            { "IsActive", "Active Status" },
            { "PermissionKeys", "Permissions" }
        };

        if (overrides.TryGetValue(propertyName, out var friendlyName))
            return friendlyName;

        // PascalCase to Spaced Case
        return Regex.Replace(propertyName, "([a-z])([A-Z])", "$1 $2");
    }

    private static string FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => "None",
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()) ? "None" : element.GetString()!,
            _ => element.GetRawText()
        };
    }
}
