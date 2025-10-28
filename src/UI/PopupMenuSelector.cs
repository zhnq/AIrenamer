using System;
using System.Runtime.InteropServices;

namespace AIRename.UI;

public static class PopupMenuSelector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    private const uint MF_STRING = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x00000100;
    private const uint TPM_LEFTALIGN = 0x00000000;
    private const uint TPM_TOPALIGN = 0x00000000;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const int SW_HIDE = 0;

    private static readonly IntPtr HInstance = GetModuleHandle(null);
    private static readonly WndProcDelegate _wndProc = new WndProcDelegate((h, m, w, l) => DefWindowProc(h, m, w, l));
    private const string OwnerClassName = "AIRename_PopupOwner";

    private static IntPtr CreateOwnerWindow()
    {
        try
        {
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = HInstance,
                lpszMenuName = null,
                lpszClassName = OwnerClassName,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                hIconSm = IntPtr.Zero
            };
            _ = RegisterClassEx(ref wc);
            var hwnd = CreateWindowEx(WS_EX_TOOLWINDOW, OwnerClassName, string.Empty, WS_POPUP,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, HInstance, IntPtr.Zero);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_HIDE);
            }
            return hwnd;
        }
        catch { return IntPtr.Zero; }
    }

    private static void DestroyOwnerWindow(IntPtr hwnd)
    {
        try { if (hwnd != IntPtr.Zero) DestroyWindow(hwnd); } catch { }
        try { UnregisterClass(OwnerClassName, HInstance); } catch { }
    }

    // 返回选中的索引(0-based)，取消返回 -1
    public static int Show(IntPtr ownerHwnd, string[] options, string? header = null)
    {
        if (options == null || options.Length == 0) return -1;
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return -1;
        IntPtr tempOwner = IntPtr.Zero;
        try
        {
            // 可选头部分隔
            if (!string.IsNullOrWhiteSpace(header))
            {
                // 灰显的标题项，不可点击
                AppendMenu(hMenu, MF_STRING | MF_DISABLED | MF_GRAYED, 0U, header);
                AppendMenu(hMenu, MF_SEPARATOR, 0U, string.Empty);
            }

            for (int i = 0; i < options.Length; i++)
            {
                var text = options[i];
                // ID 从 1 开始，避免 0 与分隔冲突
                AppendMenu(hMenu, MF_STRING, (uint)(i + 1), text);
            }

            GetCursorPos(out var pt);
            if (ownerHwnd == IntPtr.Zero)
            {
                // 创建隐藏的所有者窗口，以确保菜单附着成功
                tempOwner = CreateOwnerWindow();
                ownerHwnd = tempOwner != IntPtr.Zero ? tempOwner : GetForegroundWindow();
            }
            SetForegroundWindow(ownerHwnd);
            int cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN | TPM_TOPALIGN, pt.X, pt.Y, 0, ownerHwnd, IntPtr.Zero);
            if (cmd <= 0) return -1; // 取消
            int index = cmd - 1;
            if (index < 0 || index >= options.Length) return -1;
            return index;
        }
        finally
        {
            DestroyMenu(hMenu);
            DestroyOwnerWindow(tempOwner);
        }
    }
}