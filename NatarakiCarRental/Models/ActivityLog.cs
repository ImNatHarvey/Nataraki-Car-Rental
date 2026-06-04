namespace NatarakiCarRental.Models;

public sealed class ActivityLog
{
    public int ActivityLogId { get; set; }
    public int? UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public int? EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
