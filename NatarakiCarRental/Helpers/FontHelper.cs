namespace NatarakiCarRental.Helpers;

public static class FontHelper
{
    private const string DefaultFontFamily = "Segoe UI";

    public static Font Regular(float size = 9F)
    {
        return new Font(DefaultFontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
    }

    public static Font SemiBold(float size = 9F)
    {
        return new Font(DefaultFontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
    }

    public static Font Title(float size = 18F)
    {
        return new Font(DefaultFontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
    }

    public static Font Italic(float size = 9F)
    {
        return new Font(DefaultFontFamily, size, FontStyle.Italic, GraphicsUnit.Point);
    }
}
