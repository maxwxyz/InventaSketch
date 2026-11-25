using System;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>Rectangle in pixels (origin top left, y down).</summary>
    public sealed record PixelRect(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public double CenterX => X + Width / 2.0;
        public double CenterY => Y + Height / 2.0;

        /// <summary>True if the point (pixel indices) lies inside the rectangle.</summary>
        public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
    }

    /// <summary>
    /// Raw picture for the chart scanners: <see cref="Width"/> × <see cref="Height"/> pixels, four bytes per pixel in the order
    /// red, green, blue, alpha, row by row from the top left (index <c>4 * (y * Width + x)</c>). Hosts fill it from the
    /// clipboard, a file or a screen capture; tests draw it themselves. No validation happens here: an empty or inconsistent
    /// picture (see <see cref="IsValid"/>) makes the scanners return warnings instead of throwing. Transparent pixels are
    /// composed onto white.
    /// </summary>
    public sealed record PixelImage(int Width, int Height, byte[] Rgba)
    {
        /// <summary>True if the size is positive and the buffer holds exactly <c>4 · Width · Height</c> bytes.</summary>
        public bool IsValid => Width > 0 && Height > 0 && Rgba is not null && (long)Width * Height * 4 == Rgba.LongLength;

        /// <summary>Picture of one colour (ARGB, e.g. <c>0xFFFFFFFF</c> for white).</summary>
        public static PixelImage Blank(int width, int height, int argb)
        {
            var w = Math.Max(0, width);
            var h = Math.Max(0, height);
            var bytes = new byte[w * h * 4];
            for (var i = 0; i < w * h; i++) Put(bytes, i, argb);
            return new PixelImage(w, h, bytes);
        }

        /// <summary>Picture from ARGB integers (index <c>y * width + x</c>), as delivered by many imaging APIs.</summary>
        public static PixelImage FromArgb(int width, int height, int[] argb)
        {
            if (argb is null || width <= 0 || height <= 0 || argb.Length != width * height) return new PixelImage(Math.Max(0, width), Math.Max(0, height), Array.Empty<byte>());
            var bytes = new byte[argb.Length * 4];
            for (var i = 0; i < argb.Length; i++) Put(bytes, i, argb[i]);
            return new PixelImage(width, height, bytes);
        }

        /// <summary>ARGB of the pixel (x, y) composed onto white; white outside the picture.</summary>
        public int this[int x, int y]
        {
            get
            {
                if (!IsValid || x < 0 || y < 0 || x >= Width || y >= Height) return PixelColor.White;
                return Opaque(Rgba, y * Width + x);
            }
        }

        /// <summary>All pixels as opaque ARGB (composed onto white), index <c>y * Width + x</c>; empty if not <see cref="IsValid"/>.</summary>
        public int[] ToArgb()
        {
            if (!IsValid) return Array.Empty<int>();
            var px = new int[Width * Height];
            for (var i = 0; i < px.Length; i++) px[i] = Opaque(Rgba, i);
            return px;
        }

        private static int Opaque(byte[] rgba, int i)
        {
            int r = rgba[4 * i], g = rgba[4 * i + 1], b = rgba[4 * i + 2], a = rgba[4 * i + 3];
            if (a < 255)
            {
                r = (r * a + 255 * (255 - a) + 127) / 255;
                g = (g * a + 255 * (255 - a) + 127) / 255;
                b = (b * a + 255 * (255 - a) + 127) / 255;
            }
            return PixelColor.Rgb(r, g, b);
        }

        private static void Put(byte[] bytes, int i, int argb)
        {
            bytes[4 * i] = (byte)((argb >> 16) & 0xFF);
            bytes[4 * i + 1] = (byte)((argb >> 8) & 0xFF);
            bytes[4 * i + 2] = (byte)(argb & 0xFF);
            bytes[4 * i + 3] = (byte)((argb >> 24) & 0xFF);
        }
    }

    /// <summary>Colour helpers on opaque ARGB integers.</summary>
    public static class PixelColor
    {
        public static readonly int White = Rgb(255, 255, 255);
        public static readonly int Black = Rgb(0, 0, 0);

        /// <summary>Opaque colour from red, green and blue.</summary>
        public static int Rgb(int r, int g, int b) => unchecked((int)0xFF000000) | (r & 0xFF) << 16 | (g & 0xFF) << 8 | (b & 0xFF);

        public static int R(int argb) => (argb >> 16) & 0xFF;
        public static int G(int argb) => (argb >> 8) & 0xFF;
        public static int B(int argb) => argb & 0xFF;

        /// <summary>Largest difference of one channel between two colours.</summary>
        public static int Distance(int a, int b)
        {
            var dr = Math.Abs(R(a) - R(b));
            var dg = Math.Abs(G(a) - G(b));
            var db = Math.Abs(B(a) - B(b));
            return Math.Max(dr, Math.Max(dg, db));
        }

        /// <summary>Spread between the strongest and the weakest channel (0 for grey).</summary>
        public static int Chroma(int argb)
        {
            int r = R(argb), g = G(argb), b = B(argb);
            return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        }

        /// <summary>Colour as "#RRGGBB".</summary>
        public static string Hex(int argb) => "#" + (argb & 0xFFFFFF).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
