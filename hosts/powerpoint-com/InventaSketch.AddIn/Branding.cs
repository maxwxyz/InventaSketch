using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// The InventaSketch logo for window title bars, dialogs and the ribbon. Source: assets/branding/logo.svg, rasterised
    /// by hosts/office-web/scripts/icons.mjs into assets/branding/inventasketch.ico (PNG frames, 16–256 px), which is
    /// embedded in this assembly.
    /// </summary>
    internal static class Branding
    {
        private const string Resource = "InventaSketch.AddIn.inventasketch.ico";
        private static readonly Lazy<byte[]> Data = new(() =>
        {
            using var s = typeof(Branding).Assembly.GetManifestResourceStream(Resource)
                ?? throw new InvalidOperationException("Embedded logo missing: " + Resource);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        });

        private static readonly Lazy<IReadOnlyList<(int Size, byte[] Png)>> Frames = new(() => ReadFrames(Data.Value));
        private static readonly Lazy<Icon> Window = new(CreateWindowIcon);

        /// <summary>Icon for title bars of InventaSketch windows and dialogs (all sizes; Windows picks the right one).</summary>
        public static Icon WindowIcon => Window.Value;

        /// <summary>The logo as a bitmap of <paramref name="size"/> pixels (closest larger frame, scaled down smoothly).</summary>
        public static Bitmap Image(int size)
        {
            var frames = Frames.Value;
            var frame = frames.FirstOrDefault(f => f.Size >= size);
            if (frame.Png is null) frame = frames.Last();
            using var src = new Bitmap(new MemoryStream(frame.Png));
            if (frame.Size == size) return new Bitmap(src);
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, size, size);
            return bmp;
        }

        /// <summary>Frame sizes and PNG data of an .ico file with PNG-compressed frames.</summary>
        internal static IReadOnlyList<(int Size, byte[] Png)> ReadFrames(byte[] ico)
        {
            var count = BitConverter.ToUInt16(ico, 4);
            var list = new List<(int, byte[])>(count);
            for (var i = 0; i < count; i++)
            {
                var e = 6 + 16 * i;
                var size = ico[e] == 0 ? 256 : ico[e];
                var length = BitConverter.ToInt32(ico, e + 8);
                var offset = BitConverter.ToInt32(ico, e + 12);
                var png = new byte[length];
                Buffer.BlockCopy(ico, offset, png, 0, length);
                list.Add((size, png));
            }
            return list.OrderBy(f => f.Item1).ToList();
        }

        private static Icon CreateWindowIcon()
        {
            try
            {
                // .NET Framework 4.6+ reads PNG frames; probe it before handing the icon to a window.
                var icon = new Icon(new MemoryStream(Data.Value));
                using (icon.ToBitmap()) { }
                return icon;
            }
            catch (Exception ex)
            {
                Log.Error("Branding icon (falling back to a single frame)", ex);
                using var bmp = Image(32);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
