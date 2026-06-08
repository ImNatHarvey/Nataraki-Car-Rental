using System.Reflection;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Notifications;

public sealed class NotificationPanelControl : UserControl
{
    private readonly NotificationService _notificationService = new();
    private readonly Panel _listPanel = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = ThemeHelper.Surface
    };
    private readonly Label _emptyLabel = new()
    {
        Text = "No notifications yet.",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = FontHelper.Regular(10F),
        ForeColor = ThemeHelper.TextSecondary,
        Visible = false
    };

    public NotificationPanelControl()
    {
        // Enable double buffering for the panel to reduce flicker
        typeof(Panel).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(_listPanel, true);
        
        DoubleBuffered = true;
        
        InitializeControl();
    }

    private void InitializeControl()
    {
        Size = new Size(340, 500);
        BackColor = ThemeHelper.Border; // Acts as the border color
        Padding = new Padding(1); // 1px border thickness

        BorderedPanel container = new()
        {
            Dock = DockStyle.Fill,
            BorderColor = Color.Transparent, // Transparent because parent Padding handles the border
            BackColor = ThemeHelper.Surface
        };

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 44, // Slightly reduced height since title is gone
            Padding = new Padding(16, 0, 16, 0),
            BackColor = ThemeHelper.Surface
        };

        Button markAllRead = new()
        {
            Text = "Mark all as read",
            FlatStyle = FlatStyle.Flat,
            ForeColor = ThemeHelper.Primary,
            Font = FontHelper.Regular(8.5F),
            Dock = DockStyle.Right,
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        markAllRead.FlatAppearance.BorderSize = 0;
        markAllRead.Click += async (s, e) => {
            await _notificationService.MarkAllAsReadAsync();
            
            // Partial update instead of full rebuild
            _listPanel.SuspendLayout();
            foreach (Control control in _listPanel.Controls)
            {
                if (control is NotificationItemControl item)
                {
                    item.MarkAsReadUI();
                }
            }
            _listPanel.ResumeLayout(true);
            NotificationService.NotifyNotificationsChanged();
        };

        header.Controls.Add(markAllRead);

        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(1)
        };

        Button viewAll = new()
        {
            Text = "View All Activities",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.Primary,
            Cursor = Cursors.Hand,
            BackColor = ThemeHelper.Surface
        };
        viewAll.FlatAppearance.BorderSize = 0;
        viewAll.Click += (s, e) => {
            OnViewAllClicked?.Invoke(this, EventArgs.Empty);
        };

        footer.Controls.Add(viewAll);

        // Add to container: Top, Bottom, then Fill
        container.Controls.Add(header);
        container.Controls.Add(footer);
        container.Controls.Add(_emptyLabel);
        container.Controls.Add(_listPanel);

        // Standard WinForms Z-order for docking: 
        // Bring Fill controls to front if they overlap
        _listPanel.BringToFront();
        _emptyLabel.BringToFront();

        Controls.Add(container);
    }

    public event EventHandler? OnViewAllClicked;

    private void HandleNotificationAction(NotificationItemControl item, bool isDeleted)
    {
        if (isDeleted)
        {
            _listPanel.SuspendLayout();
            _listPanel.Controls.Remove(item);
            item.Dispose();
            
            if (_listPanel.Controls.Count == 0)
            {
                _emptyLabel.Visible = true;
                _listPanel.Visible = false;
                _emptyLabel.BringToFront();
            }
            _listPanel.ResumeLayout(true);
        }
        
        NotificationService.NotifyNotificationsChanged();
    }

    public async Task RefreshAsync()
    {
        try
        {
            _listPanel.SuspendLayout();
            
            // Dispose existing controls properly
            while (_listPanel.Controls.Count > 0)
            {
                var c = _listPanel.Controls[0];
                _listPanel.Controls.Remove(c);
                c.Dispose();
            }

            var notifications = await _notificationService.GetRecentNotificationsAsync(30);
            
            if (notifications == null || notifications.Count == 0)
            {
                _emptyLabel.Visible = true;
                _listPanel.Visible = false;
                _emptyLabel.BringToFront();
            }
            else
            {
                _emptyLabel.Visible = false;
                _listPanel.Visible = true;
                _listPanel.BringToFront();

                for (int i = notifications.Count - 1; i >= 0; i--)
                {
                    var n = notifications[i];
                    var item = new NotificationItemControl(n, HandleNotificationAction);
                    item.Dock = DockStyle.Top;
                    _listPanel.Controls.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading notifications: {ex.Message}");
        }
        finally
        {
            _listPanel.ResumeLayout(true);
        }
    }
}
