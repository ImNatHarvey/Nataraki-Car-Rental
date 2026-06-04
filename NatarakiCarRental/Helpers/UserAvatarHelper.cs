using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Helpers;

public static class UserAvatarHelper
{
    public static Image CreateAvatar(User user, int size)
    {
        Image? profileImage = LoadCircularProfileImage(user.ProfileImagePath, size);
        return profileImage ?? CreateInitialsAvatar(user.FirstName, user.LastName, size);
    }

    public static Image CreateInitialsAvatar(string? firstName, string? lastName, int size)
    {
        string initials = GetInitials(firstName, lastName);
        Bitmap bitmap = new(size, size);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        using GraphicsPath circle = new();
        circle.AddEllipse(0, 0, size - 1, size - 1);
        using SolidBrush backgroundBrush = new(ThemeHelper.Secondary);
        graphics.FillPath(backgroundBrush, circle);
        using Pen borderPen = new(Color.FromArgb(60, ThemeHelper.Primary));
        graphics.DrawPath(borderPen, circle);

        Font font = FontHelper.SemiBold(Math.Max(11F, size * 0.42F));
        using SolidBrush textBrush = new(ThemeHelper.Primary);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(initials, font, textBrush, new RectangleF(0, 0, size, size), format);
        return bitmap;
    }

    public static Image? LoadCircularProfileImage(string? storedPath, int size)
    {
        string? path = UploadPathHelper.ResolveProfileImagePath(storedPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using Image source = Image.FromStream(stream);
            Bitmap bitmap = new(size, size);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.Clear(Color.Transparent);

            using GraphicsPath circle = new();
            circle.AddEllipse(0, 0, size - 1, size - 1);
            graphics.SetClip(circle);

            Rectangle destination = new(0, 0, size, size);
            Rectangle sourceRect = GetCenteredSquare(source.Width, source.Height);
            graphics.DrawImage(source, destination, sourceRect, GraphicsUnit.Pixel);
            graphics.ResetClip();

            using Pen borderPen = new(ThemeHelper.Border);
            graphics.DrawEllipse(borderPen, 0, 0, size - 1, size - 1);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static Rectangle GetCenteredSquare(int width, int height)
    {
        int side = Math.Min(width, height);
        return new Rectangle((width - side) / 2, (height - side) / 2, side, side);
    }

    private static string GetInitials(string? firstName, string? lastName)
    {
        char? firstInitial = GetFirstLetter(firstName);
        if (firstInitial.HasValue)
        {
            return firstInitial.Value.ToString().ToUpperInvariant();
        }

        char? lastInitial = GetFirstLetter(lastName);
        return lastInitial?.ToString().ToUpperInvariant() ?? "?";
    }

    private static char? GetFirstLetter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim()[0];
    }
}
