using System;
using System.Runtime.InteropServices;
using System.Text;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>Win32-Aufrufe für Hooks, Overlay-Fenster und Zeiger. Nur was die Bearbeitung auf der Folie braucht.</summary>
    internal static class NativeMethods
    {
        public const int WH_KEYBOARD = 2;
        public const int WH_MOUSE = 7;
        public const int HC_ACTION = 0;

        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_RBUTTONDBLCLK = 0x0206;

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_LEFT = 0x25;
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;
        public const int VK_DELETE = 0x2E;
        public const int VK_BACK = 0x08;

        /// <summary>Entf oder Rücktaste: beide löschen die Auswahl (B175, wie in der Web- und der LibreOffice-Fassung).</summary>
        public static bool IsDeleteKey(int vk) => vk == VK_DELETE || vk == VK_BACK;
        public const int VK_F2 = 0x71;
        public const int VK_TAB = 0x09;
        public const int VK_D = 0x44;
        public const int VK_A = 0x41;
        public const int VK_SPACE = 0x20;
        public const int VK_RETURN = 0x0D;

        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int ULW_ALPHA = 0x00000002;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const int GWLP_HWNDPARENT = -8;
        public const uint GA_ROOT = 2;
        public const uint GW_OWNER = 4;

        public const int IDC_ARROW = 32512;
        public const int IDC_CROSS = 32515;
        public const int IDC_SIZENWSE = 32642;
        public const int IDC_SIZENESW = 32643;
        public const int IDC_SIZEWE = 32644;
        public const int IDC_SIZENS = 32645;
        public const int IDC_SIZEALL = 32646;
        public const int IDC_NO = 32648;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int Cx;
            public int Cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEHOOKSTRUCT
        {
            public POINT Pt;
            public IntPtr Hwnd;
            public uint HitTestCode;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;
        public const int VK_MBUTTON = 0x04;
        public const int VK_V = 0x56;

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr GetCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        /// <summary>Monitor, der das Rechteck überwiegend zeigt (MONITOR_DEFAULTTONEAREST = 2).</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

        /// <summary>Wirksame DPI eines Monitors (Typ 0); echte Werte nur mit Skalierung je Monitor (Windows 8.1).</summary>
        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>Fenstermeldung bei Wechsel auf einen Monitor mit anderer Skalierung.</summary>
        public const int WM_DPICHANGED = 0x02E0;

        /// <summary>Skalierung je Monitor, Version 2 (Windows 10 1703).</summary>
        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        public static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LogicalToPhysicalPointForPerMonitorDPI(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        /// <summary>
        /// Oberstes Besitzerfenster (Dialoge, Leiste, Editor -> PowerPoint-Hauptfenster). GA_ROOTOWNER folgt dem Besitzer nur
        /// bei Popup-Fenstern, WinForms-Dialoge sind keine; daher selbst über GW_OWNER gehen.
        /// </summary>
        public static IntPtr RootOwner(IntPtr hwnd)
        {
            var w = GetAncestor(hwnd, GA_ROOT);
            for (int i = 0; i < 16 && w != IntPtr.Zero; i++)
            {
                var owner = GetWindow(w, GW_OWNER);
                if (owner == IntPtr.Zero) break;
                w = owner;
            }
            return w;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        public static void SetOwner(IntPtr hwnd, IntPtr owner)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(hwnd, GWLP_HWNDPARENT, owner.ToInt32());
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc,
            ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        /// <summary>Farbe eines Bildschirmpunkts als COLORREF (0x00BBGGRR); 0xFFFFFFFF außerhalb.</summary>
        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hdc, int x, int y);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);

        /// <summary>
        /// Zeichen einer gedrückten Taste nach aktuellem Tastaturlayout, oder null (Steuertasten, tote Tasten).
        /// Flag 4: Zustand toter Tasten nicht verändern (Windows 10 1607 und neuer).
        /// </summary>
        public static string? CharOf(int vk)
        {
            var state = new byte[256];
            if (!GetKeyboardState(state)) return null;
            var sb = new StringBuilder(4);
            int n = ToUnicode((uint)vk, MapVirtualKey((uint)vk, 0), state, sb, sb.Capacity, 4);
            if (n <= 0) return null;
            string text = sb.ToString(0, n);
            return text.Length > 0 && !char.IsControl(text[0]) ? text : null;
        }

        public static string ClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(64);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool KeyDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;
    }
}
