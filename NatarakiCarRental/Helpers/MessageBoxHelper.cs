using FontAwesome.Sharp;
using NatarakiCarRental.Forms.Common;
using System.Windows.Forms;

namespace NatarakiCarRental.Helpers;

public static class MessageBoxHelper
{
    public static void ShowSuccess(string message, string title = "Success")
    {
        ShowDialog(message, title, IconChar.CircleCheck, ThemeHelper.Success);
    }

    public static void ShowWarning(string message, string title = "Warning")
    {
        ShowDialog(message, title, IconChar.TriangleExclamation, ThemeHelper.Warning);
    }

    public static void ShowInfo(string message, string title = "Information")
    {
        ShowDialog(message, title, IconChar.CircleInfo, ThemeHelper.Primary);
    }

    public static void ShowError(string message, string title = "Error")
    {
        ShowDialog(message, title, IconChar.CircleXmark, ThemeHelper.Error);
    }

    public static void ShowDatabaseError(Exception exception)
    {
        ShowError($"Database connection failed.\n\n{exception.Message}");
    }

    public static bool Confirm(string message, string? title = null)
    {
        return ShowConfirmDanger(message, title ?? AppBrandingManager.CurrentSettings.BusinessName);
    }

    public static bool ShowConfirmWarning(string message, string title = "Warning")
    {
        return ShowConfirmation(message, title, IconChar.TriangleExclamation, ThemeHelper.Warning);
    }

    public static bool ShowConfirmDanger(string message, string title = "Confirm Action")
    {
        return ShowConfirmation(message, title, IconChar.TriangleExclamation, ThemeHelper.Danger);
    }

    private static void ShowDialog(string message, string title, IconChar icon, Color accentColor)
    {
        using AppMessageDialog dialog = new(title, message, icon, accentColor);
        IWin32Window? owner = Form.ActiveForm;

        if (owner is null)
        {
            dialog.StartPosition = FormStartPosition.CenterScreen;
            dialog.ShowDialog();
            return;
        }

        dialog.ShowDialog(owner);
    }

    private static bool ShowConfirmation(string message, string title, IconChar icon, Color accentColor)
    {
        using AppMessageDialog dialog = new(title, message, icon, accentColor, isConfirmation: true);
        IWin32Window? owner = Form.ActiveForm;

        if (owner is null)
        {
            dialog.StartPosition = FormStartPosition.CenterScreen;
            return dialog.ShowDialog() == DialogResult.Yes;
        }

        return dialog.ShowDialog(owner) == DialogResult.Yes;
    }
}
