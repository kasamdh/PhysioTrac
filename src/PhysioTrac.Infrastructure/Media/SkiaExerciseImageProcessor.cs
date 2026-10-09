using PhysioTrac.Application.Clinical;
using SkiaSharp;

namespace PhysioTrac.Infrastructure.Media;

/// <summary>Validates and re-encodes exercise images with SkiaSharp. The
/// file's own bytes decide what it is: the name and the browser's content
/// type are ignored. Dimensions are read before decoding, so an oversized
/// ("decompression bomb") file is refused without allocating it. The result
/// is a fresh encode -- no EXIF/GPS metadata, comments or trailing data
/// survive -- as PNG when the image has transparency (illustrations) and
/// JPEG otherwise (photos).</summary>
public class SkiaExerciseImageProcessor : IExerciseImageProcessor
{
    public ProcessedImage Process(byte[] upload)
    {
        if (upload.Length == 0) throw new InvalidOperationException("The file is empty.");
        if (upload.Length > ExerciseRules.MaxImageBytes)
            throw new InvalidOperationException($"Images can be up to {ExerciseRules.MaxImageBytes / (1024 * 1024)} MB.");
        if (!LooksLikeSupportedImage(upload))
            throw new InvalidOperationException("Upload a JPEG, PNG or WebP image.");

        using var data = SKData.CreateCopy(upload);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidOperationException("The file isn't a readable image.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new InvalidOperationException("Upload a JPEG, PNG or WebP image.");

        var info = codec.Info;
        if (info.Width > ExerciseRules.MaxImageSide || info.Height > ExerciseRules.MaxImageSide)
            throw new InvalidOperationException($"Images can be up to {ExerciseRules.MaxImageSide} pixels on a side.");
        if (info.Width < ExerciseRules.MinImageSide || info.Height < ExerciseRules.MinImageSide)
            throw new InvalidOperationException($"Images must be at least {ExerciseRules.MinImageSide} pixels on a side.");

        var decodeInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888,
            info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new InvalidOperationException("The file isn't a readable image.");

        using var upright = Orient(decoded, codec.EncodedOrigin);
        var hasAlpha = info.AlphaType != SKAlphaType.Opaque && HasTransparentPixels(upright);

        var (display, width, height) = Encode(upright, ExerciseRules.DisplaySide, hasAlpha);
        var (thumbnail, _, _) = Encode(upright, ExerciseRules.ThumbnailSide, hasAlpha);
        return new ProcessedImage(display, thumbnail, hasAlpha ? "image/png" : "image/jpeg", width, height);
    }

    /// <summary>JPEG (FF D8 FF), PNG (89 'PNG' 0D 0A 1A 0A) or WebP ("RIFF" then "WEBP").</summary>
    public static bool LooksLikeSupportedImage(ReadOnlySpan<byte> b) =>
        (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) ||
        (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) ||
        (b.Length > 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F' &&
         b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P');

    private static (byte[] Bytes, int Width, int Height) Encode(SKBitmap source, int maxSide, bool png)
    {
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(source.Width, source.Height));
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = scale < 1.0
            ? source.Resize(new SKImageInfo(w, h, source.ColorType, source.AlphaType), new SKSamplingOptions(SKCubicResampler.Mitchell))
            : source.Copy();
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, png ? 100 : 88)
            ?? throw new InvalidOperationException("The image couldn't be processed.");
        return (encoded.ToArray(), w, h);
    }

    /// <summary>Applies the camera's EXIF orientation, so photos taken on a
    /// phone aren't shown sideways once the metadata is stripped.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return source.Copy();
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(new SKImageInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height,
            source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Scale(-1, 1, source.Width / 2f, 0); break;
            case SKEncodedOrigin.BottomRight: canvas.RotateDegrees(180, source.Width / 2f, source.Height / 2f); break;
            case SKEncodedOrigin.BottomLeft: canvas.Scale(1, -1, 0, source.Height / 2f); break;
            case SKEncodedOrigin.LeftTop: canvas.Translate(0, source.Width); canvas.RotateDegrees(-90); canvas.Scale(-1, 1, source.Width / 2f, 0); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(source.Height, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(source.Height, 0); canvas.RotateDegrees(90); canvas.Scale(-1, 1, source.Width / 2f, 0); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, source.Width); canvas.RotateDegrees(-90); break;
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static bool HasTransparentPixels(SKBitmap bitmap)
    {
        // Sample a grid: enough to tell an illustration with a transparent
        // background from a photo saved as PNG.
        var stepX = Math.Max(1, bitmap.Width / 64);
        var stepY = Math.Max(1, bitmap.Height / 64);
        for (var y = 0; y < bitmap.Height; y += stepY)
            for (var x = 0; x < bitmap.Width; x += stepX)
                if (bitmap.GetPixel(x, y).Alpha < 255) return true;
        return false;
    }
}
