using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BMIRussian_ru.Services;

/// <summary>
/// Renders engraving text into a PNG (black text on white background) consumed by OpenSCAD surface(invert=true).
/// </summary>
public class TextToPngRenderer : IDisposable
{
    private const int Padding = 10;
    private readonly SKFont _font;
    private readonly object _lock = new();

    public TextToPngRenderer(IWebHostEnvironment env, IOptions<ShopOptions> options)
    {
        var fontPath = options.Value.FontPath;
        if (!Path.IsPathRooted(fontPath))
            fontPath = Path.Combine(env.ContentRootPath, fontPath);
        var typeface = SKTypeface.FromFile(fontPath)
            ?? throw new InvalidOperationException($"Failed to load font '{fontPath}'.");
        _font = new SKFont(typeface, options.Value.FontSize);
    }

    /// <summary>Renders the text into a PNG. Returns PNG bytes and image dimensions.</summary>
    public byte[] Render(string text, out int width, out int height)
    {
        lock (_lock)
        {
            var textWidth = _font.MeasureText(text);
            var metrics = _font.Metrics;
            width = (int)Math.Ceiling(textWidth) + Padding * 2;
            height = (int)Math.Ceiling(-metrics.Ascent + metrics.Descent) + Padding * 2;

            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                canvas.DrawText(text, Padding, -metrics.Ascent + Padding, SKTextAlign.Left, _font, paint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }

    public void Dispose() => _font.Dispose();
}
