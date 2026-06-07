using System.Data;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class NotificationService
{
    private readonly NotificationRepository _repository;
    
    public static event EventHandler? NotificationsChanged;

    public NotificationService() : this(new NotificationRepository())
    {
    }

    public NotificationService(NotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> NotifyAsync(string title, string message, string type = "Info", int? entityId = null, string? module = null, IDbTransaction? transaction = null)
    {
        var notification = new Notification
        {
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityId = entityId,
            RelatedModule = module
        };

        int id = await _repository.AddAsync(notification, transaction);
        NotificationsChanged?.Invoke(null, EventArgs.Empty);
        return id;
    }

    public Task<IReadOnlyList<Notification>> GetRecentNotificationsAsync(int limit = 20)
    {
        return _repository.GetLatestAsync(limit);
    }

    public Task<int> GetUnreadCountAsync()
    {
        return _repository.GetUnreadCountAsync();
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        await _repository.MarkAsReadAsync(notificationId);
        NotificationsChanged?.Invoke(null, EventArgs.Empty);
    }

    public async Task MarkAllAsReadAsync()
    {
        // System notifications are global. Only admins should be able to mark all as read.
        AccessControlService.EnforcePermission("ManageSystem.Settings");
        await _repository.MarkAllAsReadAsync();
        NotificationsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void NotifyNotificationsChanged()
    {
        NotificationsChanged?.Invoke(null, EventArgs.Empty);
    }

    public async Task DeleteAsync(int notificationId)
    {
        // System notifications are global. Only admins should be able to delete them.
        AccessControlService.EnforcePermission("ManageSystem.Settings");
        await _repository.DeleteAsync(notificationId);
        NotificationsChanged?.Invoke(null, EventArgs.Empty);
    }
}
