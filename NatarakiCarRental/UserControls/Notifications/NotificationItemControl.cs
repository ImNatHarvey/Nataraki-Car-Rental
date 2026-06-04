using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.Notifications;

public sealed class NotificationItemControl : UserControl
{
    private readonly Notification _notification;
    private readonly NotificationService _notificationService = new();
    private readonly Action _onChanged;
    private readonly IconButton _actionButton = new();

    public NotificationItemControl(Notification notification, Action onChanged)
    {
        _notification = notification;
        _onChanged = onChanged;
        InitializeControl();
    }

    private void InitializeControl()
    {
        Dock = DockStyle.Top;
        Height = 72;
        MinimumSize = new Size(0, 72);
        // Use a very light blue for unread, white for read
        BackColor = _notification.IsRead ? ThemeHelper.Surface : Color.FromArgb(245, 248, 255);
        Padding = new Padding(0);
        Cursor = Cursors.Hand;

        Color accentColor = _notification.Type switch
        {
            "Success" => ThemeHelper.Success,
            "Warning" => ThemeHelper.Warning,
            "Danger" => ThemeHelper.Danger,
            _ => ThemeHelper.Primary
        };

        Panel accentBar = new()
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = accentColor
        };

        Label titleLabel = new()
        {
            Text = _notification.Title,
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            AutoSize = true,
            Location = new Point(16, 12),
            BackColor = Color.Transparent,
            UseMnemonic = false
        };

        Label messageLabel = new()
        {
            Text = _notification.Message,
            Font = FontHelper.Regular(8.5F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoSize = false,
            Size = new Size(240, 18), // Reduced to make room for action button
            Location = new Point(16, 32),
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };

        Label timeLabel = new()
        {
            Text = GetRelativeTime(_notification.CreatedAt),
            Font = FontHelper.Regular(8F),
            ForeColor = ThemeHelper.GrayIcon,
            AutoSize = true,
            Location = new Point(16, 52),
            BackColor = Color.Transparent,
            UseMnemonic = false
        };

        _actionButton.Size = new Size(32, 32);
        _actionButton.Location = new Point(290, 20);
        _actionButton.FlatStyle = FlatStyle.Flat;
        _actionButton.FlatAppearance.BorderSize = 0;
        _actionButton.BackColor = Color.Transparent;
        _actionButton.Cursor = Cursors.Hand;
        _actionButton.IconSize = 18;
        
        UpdateActionButtonState();

        _actionButton.Click += ActionButton_Click;

        Panel bottomBorder = new()
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Color.FromArgb(242, 242, 242)
        };

        Controls.Add(accentBar);
        Controls.Add(titleLabel);
        Controls.Add(messageLabel);
        Controls.Add(timeLabel);
        Controls.Add(_actionButton);
        Controls.Add(bottomBorder);

        // Ensure sub-controls don't block clicks, except the action button
        foreach (Control c in Controls)
        {
            if (c == _actionButton) continue;

            c.Click += async (s, e) => await HandleItemClick();
            c.MouseEnter += NotificationItem_MouseEnter;
            c.MouseLeave += NotificationItem_MouseLeave;
        }

        Click += async (s, e) => await HandleItemClick();
        MouseEnter += NotificationItem_MouseEnter;
        MouseLeave += NotificationItem_MouseLeave;
    }

    private void UpdateActionButtonState()
    {
        if (!_notification.IsRead)
        {
            _actionButton.IconChar = IconChar.Check;
            _actionButton.IconColor = ThemeHelper.Success;
            ToolTip tip = new();
            tip.SetToolTip(_actionButton, "Mark as read");
        }
        else
        {
            _actionButton.IconChar = IconChar.Xmark;
            _actionButton.IconColor = ThemeHelper.TextSecondary;
            ToolTip tip = new();
            tip.SetToolTip(_actionButton, "Delete notification");
        }
    }

    private async void ActionButton_Click(object? sender, EventArgs e)
    {
        if (!_notification.IsRead)
        {
            await _notificationService.MarkAsReadAsync(_notification.NotificationId);
        }
        else
        {
            await _notificationService.DeleteAsync(_notification.NotificationId);
        }
        _onChanged();
    }

    private async Task HandleItemClick()
    {
        if (!_notification.IsRead)
        {
            await _notificationService.MarkAsReadAsync(_notification.NotificationId);
            _onChanged();
        }
    }

    private void NotificationItem_MouseEnter(object? sender, EventArgs e)
    {
        BackColor = Color.FromArgb(240, 244, 255);
    }

    private void NotificationItem_MouseLeave(object? sender, EventArgs e)
    {
        BackColor = _notification.IsRead ? ThemeHelper.Surface : Color.FromArgb(245, 248, 255);
    }

    private static string GetRelativeTime(DateTime dateTime)
    {
        TimeSpan span = DateTime.Now - dateTime;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} mins ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        return dateTime.ToString("MMM dd");
    }
}
