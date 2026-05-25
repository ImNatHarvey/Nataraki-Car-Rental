using System.Collections.Concurrent;

namespace NatarakiCarRental.Helpers;

public static class FontHelper
{
    private const string DefaultFontFamily = "Segoe UI";
    private static readonly ConcurrentDictionary<(string, float, FontStyle), Font> _fontCache = new();

    public static Font Regular(float size = 9F)
    {
        return GetCachedFont(DefaultFontFamily, size, FontStyle.Regular);
    }

    public static Font SemiBold(float size = 9F)
    {
        return GetCachedFont(DefaultFontFamily, size, FontStyle.Bold);
    }

    public static Font Title(float size = 18F)
    {
        return GetCachedFont(DefaultFontFamily, size, FontStyle.Bold);
    }

    public static Font Italic(float size = 9F)
    {
        return GetCachedFont(DefaultFontFamily, size, FontStyle.Italic);
    }

    private static Font GetCachedFont(string family, float size, FontStyle style)
    {
        return _fontCache.GetOrAdd((family, size, style), key => new Font(key.Item1, key.Item2, key.Item3, GraphicsUnit.Point));
    }
}
