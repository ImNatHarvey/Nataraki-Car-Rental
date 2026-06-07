using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.Notifications;

public sealed class NotificationBell : UserControl
{
    private readonly NotificationService _notificationService = new();
    private readonly IconPictureBox _bellIcon = new();
    private readonly Label _badgeLabel = new();
    private int _unreadCount = 0;

    public NotificationBell()
    {
        InitializeControl();
        NotificationService.NotificationsChanged += NotificationService_NotificationsChanged;
        Load += async (s, e) => await UpdateBadgeAsync();
        Disposed += (s, e) => NotificationService.NotificationsChanged -= NotificationService_NotificationsChanged;
    }

    private async void NotificationService_NotificationsChanged(object? sender, EventArgs e)
    {
        await UpdateBadgeAsync();
    }

    private void InitializeControl()
    {
        Size = new Size(34, 34);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;

        _bellIcon.IconChar = IconChar.Bell;
        _bellIcon.IconColor = ThemeHelper.TextSecondary;
        _bellIcon.IconSize = 24;
        _bellIcon.Dock = DockStyle.Fill;
        _bellIcon.SizeMode = PictureBoxSizeMode.CenterImage;
        _bellIcon.BackColor = Color.Transparent;
        _bellIcon.Cursor = Cursors.Hand;

        _badgeLabel.AutoSize = false;
        _badgeLabel.Size = new Size(16, 16);
        _badgeLabel.Location = new Point(16, 2);
        _badgeLabel.BackColor = ThemeHelper.Danger;
        _badgeLabel.ForeColor = Color.White;
        _badgeLabel.Font = FontHelper.SemiBold(7.5F);
        _badgeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _badgeLabel.Visible = false;
        
        // Circular badge
        ControlFactory.ApplyRoundedPanel(_badgeLabel);

        Controls.Add(_badgeLabel);
        Controls.Add(_bellIcon);

        foreach (Control c in Controls)
        {
            c.Click += (s, e) => OnClick(e);
            c.MouseEnter += Bell_MouseEnter;
            c.MouseLeave += Bell_MouseLeave;
        }

        MouseEnter += Bell_MouseEnter;
        MouseLeave += Bell_MouseLeave;
    }

    private void Bell_MouseEnter(object? sender, EventArgs e)
    {
        _bellIcon.IconColor = ThemeHelper.Primary;
    }

    private void Bell_MouseLeave(object? sender, EventArgs e)
    {
        _bellIcon.IconColor = ThemeHelper.TextSecondary;
    }

    private async Task UpdateBadgeAsync()
    {
        if (IsDisposed) return;

        _unreadCount = await _notificationService.GetUnreadCountAsync();
        
        if (InvokeRequired)
        {
            Invoke(new Action(() => UpdateBadgeUI()));
        }
        else
        {
            UpdateBadgeUI();
        }
    }

    private void UpdateBadgeUI()
    {
        if (_unreadCount > 0)
        {
            string badgeText;
            if (_unreadCount < 10)
                badgeText = _unreadCount.ToString();
            else if (_unreadCount < 100)
                badgeText = _unreadCount.ToString();
            else
                badgeText = "99+";

            _badgeLabel.Text = badgeText;
            
            using (Graphics g = _badgeLabel.CreateGraphics())
            {
                SizeF textSize = g.MeasureString(badgeText, _badgeLabel.Font);
                int padding = 6;
                int width = Math.Max(16, (int)textSize.Width + padding);
                
                _badgeLabel.Size = new Size(width, 16);
                _badgeLabel.Location = new Point(Width - width - 2, 2);
                
                ControlFactory.ApplyRoundedPanel(_badgeLabel);
            }
            
            _badgeLabel.Visible = true;
        }
        else
        {
            _badgeLabel.Visible = false;
        }
    }
}
