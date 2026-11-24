using System;
using System.Runtime.InteropServices;
using static InventaSketch.AddIn.OnSlide.NativeMethods;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Thread-local mouse and keyboard hooks on PowerPoint's UI thread (WH_MOUSE, WH_KEYBOARD, no DLL injection).
    /// Rules learned in Slidemax:
    /// - callbacks run inside message retrieval: only compute and draw; object-model calls go through BeginInvoke;
    /// - the delegates are kept in fields, otherwise the GC collects them while Windows still calls them;
    /// - only messages for the slide pane may be swallowed; a press that PowerPoint received also gets its release;
    /// - an exception in a callback never blocks input and ends all modes.
    /// </summary>
    internal sealed class InputHooks : IDisposable
    {
        private readonly HookProc _mouseProc;
        private readonly HookProc _keyboardProc;
        private IntPtr _mouseHook;
        private IntPtr _keyboardHook;
        private bool _hostOwnsLeft;
        private bool _hostOwnsRight;
        // The add-in swallowed the press: the release must be swallowed too, or PowerPoint gets an unpaired release
        // (an unpaired right-button release makes Windows open PowerPoint's own context menu).
        private bool _addinOwnsLeft;
        private bool _addinOwnsRight;
        private bool _uninstallPending;

        public InputHooks()
        {
            _mouseProc = MouseProc;
            _keyboardProc = KeyboardProc;
        }

        /// <summary>(message, screen point in physical pixels, window under the pointer) → swallow?</summary>
        public Func<int, POINT, IntPtr, bool>? Mouse { get; set; }

        /// <summary>(virtual key, shift, ctrl, alt) → swallow?</summary>
        public Func<int, bool, bool, bool, bool>? KeyDown { get; set; }

        /// <summary>Called after an exception so modes do not stay half active.</summary>
        public Action? Reset { get; set; }

        public bool Installed => _mouseHook != IntPtr.Zero;

        public void Install()
        {
            // A pending uninstall (waiting for a release) is cancelled when the hooks are wanted again.
            _uninstallPending = false;
            if (Installed) return;
            uint thread = GetCurrentThreadId();
            // A button already held while installing was pressed for PowerPoint: its release belongs to PowerPoint.
            _hostOwnsLeft = GetAsyncKeyState(VK_LBUTTON) < 0;
            _hostOwnsRight = GetAsyncKeyState(VK_RBUTTON) < 0;
            _addinOwnsLeft = _addinOwnsRight = false;
            _mouseHook = SetWindowsHookEx(WH_MOUSE, _mouseProc, IntPtr.Zero, thread);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD, _keyboardProc, IntPtr.Zero, thread);
            if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero) Log.Info("Hook could not be installed, error " + Marshal.GetLastWin32Error());
        }

        /// <summary>Only the keyboard hook (a permanent shortcut such as Ctrl+Space; the mouse stays untouched).</summary>
        public void InstallKeyboard()
        {
            if (_keyboardHook != IntPtr.Zero) return;
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD, _keyboardProc, IntPtr.Zero, GetCurrentThreadId());
            if (_keyboardHook == IntPtr.Zero) Log.Info("Keyboard hook could not be installed, error " + Marshal.GetLastWin32Error());
        }

        public void Uninstall()
        {
            // Keep the mouse hook until a button pressed for the add-in is released.
            if (_mouseHook != IntPtr.Zero && (_addinOwnsLeft || _addinOwnsRight))
            {
                _uninstallPending = true;
                return;
            }
            _uninstallPending = false;
            if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
            if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
            _mouseHook = _keyboardHook = IntPtr.Zero;
        }

        public void Dispose() => Uninstall();

        private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                int message = wParam.ToInt32();
                bool swallow = false;
                try
                {
                    var data = Marshal.PtrToStructure<MOUSEHOOKSTRUCT>(lParam);
                    var pt = DpiScope.ToPhysical(data.Hwnd, data.Pt);
                    if (Mouse is not null)
                        using (DpiScope.PerMonitor())
                            swallow = Mouse(message, pt, data.Hwnd);
                }
                catch (Exception ex)
                {
                    Log.Error("MouseHook", ex);
                    swallow = false;
                    try { Reset?.Invoke(); } catch (Exception inner) { Log.Error("Reset", inner); }
                }
                // Press/release pairing: whoever got the press also gets the release.
                switch (message)
                {
                    case WM_LBUTTONDOWN: case WM_LBUTTONDBLCLK: _hostOwnsLeft = !swallow; _addinOwnsLeft = swallow; break;
                    case WM_RBUTTONDOWN: case WM_RBUTTONDBLCLK: _hostOwnsRight = !swallow; _addinOwnsRight = swallow; break;
                    case WM_LBUTTONUP:
                        swallow = !_hostOwnsLeft && (swallow || _addinOwnsLeft);
                        _hostOwnsLeft = _addinOwnsLeft = false;
                        break;
                    case WM_RBUTTONUP:
                        swallow = !_hostOwnsRight && (swallow || _addinOwnsRight);
                        _hostOwnsRight = _addinOwnsRight = false;
                        break;
                }
                if (_uninstallPending && !_addinOwnsLeft && !_addinOwnsRight && message is WM_LBUTTONUP or WM_RBUTTONUP)
                {
                    var result = swallow ? new IntPtr(1) : CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                    Uninstall();
                    return result;
                }
                if (swallow) return new IntPtr(1);
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // lParam bit 31: 0 = pressed, 1 = released.
            if (nCode == HC_ACTION && KeyDown is not null && (lParam.ToInt64() & 0x80000000L) == 0)
            {
                try
                {
                    // Keys typed into the ribbon or a text box belong to them; only Esc always reaches our modes.
                    var focus = ClassName(GetFocus());
                    int vk = wParam.ToInt32();
                    bool slideFocus = focus == "paneClassDC" || focus == "mdiClass";
                    if (slideFocus || vk == VK_ESCAPE)
                    {
                        bool swallow;
                        using (DpiScope.PerMonitor())
                            swallow = KeyDown(vk, IsDown(VK_SHIFT), IsDown(VK_CONTROL), IsDown(VK_MENU));
                        if (swallow && slideFocus) return new IntPtr(1);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("KeyboardHook", ex);
                    try { Reset?.Invoke(); } catch (Exception inner) { Log.Error("Reset", inner); }
                }
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static bool IsDown(int vk) => NativeMethods.KeyDown(vk);
    }
}
