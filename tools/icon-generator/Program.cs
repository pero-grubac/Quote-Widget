// Generates QuoteWidget/Assets/app.ico: a quotation-mark glyph on a dark rounded square.
// Re-run with: dotnet run --project tools/icon-generator
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var backgroundColor = ColorTranslator.FromHtml("#1C1C22");
var glyphColor = ColorTranslator.FromHtml("#F2F0E8");

var pngFrames = new List<(int Size, byte[] Png)>();

foreach (var size in sizes)
{
    using var bitmap = new Bitmap(size, size);
    using (var g = Graphics.FromImage(bitmap))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        var cornerRadius = size * 0.22f;
        using (var path = RoundedRect(new RectangleF(0, 0, size, size), cornerRadius))
        using (var brush = new SolidBrush(backgroundColor))
        {
            g.FillPath(brush, path);
        }

        using var font = new Font("Georgia", size * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(glyphColor);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        // Nudge up slightly: the glyph's visual weight sits above the font's own vertical center.
        var textRect = new RectangleF(0, -size * 0.06f, size, size);
        g.DrawString("“", font, textBrush, textRect, format);
    }

    using var stream = new MemoryStream();
    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
    pngFrames.Add((size, stream.ToArray()));
}

var outputPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "QuoteWidget", "Assets", "app.ico");
outputPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
WriteIco(outputPath, pngFrames);

Console.WriteLine($"Wrote {outputPath}");

static GraphicsPath RoundedRect(RectangleF bounds, float radius)
{
    var diameter = radius * 2;
    var path = new GraphicsPath();

    path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
    path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
    path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
    path.CloseFigure();
    return path;
}

static void WriteIco(string path, List<(int Size, byte[] Png)> frames)
{
    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(fs);

    writer.Write((ushort)0); // reserved
    writer.Write((ushort)1); // type = icon
    writer.Write((ushort)frames.Count);

    var headerSize = 6 + frames.Count * 16;
    var offset = headerSize;

    foreach (var (size, png) in frames)
    {
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0); // color count (0 = no palette, truecolor)
        writer.Write((byte)0); // reserved
        writer.Write((ushort)1); // color planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write(png.Length);
        writer.Write(offset);
        offset += png.Length;
    }

    foreach (var (_, png) in frames)
    {
        writer.Write(png);
    }
}
