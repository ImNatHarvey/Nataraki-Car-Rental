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
        InitializeControl();
    }

    private void InitializeControl()
    {
        Size = new Size(340, 500);
        BackColor = ThemeHelper.Surface;
        Padding = new Padding(1); // For border effect if needed

        BorderedPanel container = new()
        {
            Dock = DockStyle.Fill,
            BorderColor = ThemeHelper.Border,
            BackColor = ThemeHelper.Surface
        };

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(16, 0, 16, 0),
            BackColor = ThemeHelper.Surface
        };

        Label titleLabel = new()
        {
            Text = "Notifications",
            Font = FontHelper.SemiBold(11.5F),
            ForeColor = ThemeHelper.TextPrimary,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
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
            await RefreshAsync();
        };

        header.Controls.Add(titleLabel);
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

    public async Task RefreshAsync()
    {
        try
        {
            _listPanel.SuspendLayout();
            _listPanel.Controls.Clear();

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

                // To have newest at the top with Dock = Top, we add oldest first, then newer on top
                // OR add in order and use SendToBack/BringToFront.
                // Most reliable newest-first with Dock=Top: Add oldest first.
                for (int i = notifications.Count - 1; i >= 0; i--)
                {
                    var n = notifications[i];
                    var item = new NotificationItemControl(n, async () => {
                        await RefreshAsync();
                    });
                    item.Dock = DockStyle.Top;
                    item.Width = 330; // Reliable width for 340 container
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
            _listPanel.PerformLayout();
        }
    }
}
