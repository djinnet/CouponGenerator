using SkiaSharp;

namespace CouponSheetGenerator;

public static class LocalImage
{
    public static byte[] ReadValidated(string path)
    {
        Validate(path);
        return File.ReadAllBytes(path);
    }

    public static void Validate(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is < 8 or > 10 * 1024 * 1024)
        {
            throw new ArgumentException("Image must be an existing local file of 10 MiB or less.");
        }

        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8];
        if (stream.Read(signature) < 8 || !(signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || (signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF)))
        {
            throw new ArgumentException("Image must be a PNG or JPEG file.");
        }

        stream.Position = 0;
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width is < 1 or > 4096 || codec.Info.Height is < 1 or > 4096)
        {
            throw new ArgumentException("Image must be valid and no larger than 4096 × 4096 pixels.");
        }
    }
}
