using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using static InventaSketch.AddIn.OnSlide.NativeMethods;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Click-through, per-pixel-alpha window over the slide pane (WS_EX_LAYERED | TRANSPARENT | TOOLWINDOW | NOACTIVATE),
    /// owned by PowerPoint's main window and drawn with UpdateLayeredWindow. It never takes focus or input; the hooks
    /// decide what happens with mouse and keyboard.
    /// </summary>
    internal sealed class OverlayWindow : Form
    {
        public OverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        /// <summary>Shows the bitmap (32bpp ARGB) at the given screen rectangle above the owner.</summary>
        public void Present(Bitmap bitmap, Rectangle screen, IntPtr owner)
        {
            if (!IsHandleCreated) CreateHandle();
            SetOwner(Handle, owner);
            var screenDc = GetDC(IntPtr.Zero);
            var memDc = CreateCompatibleDC(screenDc);
            var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            var old = SelectObject(memDc, hBitmap);
            try
            {
                var size = new SIZE { Cx = bitmap.Width, Cy = bitmap.Height };
                var src = new POINT { X = 0, Y = 0 };
                var dst = new POINT { X = screen.X, Y = screen.Y };
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
                if (!Visible) Show();
                SetWindowPos(Handle, HWND_TOP, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /* NOSIZE | NOMOVE | NOACTIVATE */);
            }
            finally
            {
                SelectObject(memDc, old);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public void Clear()
        {
            if (Visible) Hide();
        }

        /// <summary>New transparent bitmap of the given size (premultiplied alpha comes from GetHbitmap).</summary>
        public static Bitmap Canvas(Size size) => new(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppArgb);
    }
}
