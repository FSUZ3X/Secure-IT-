using System;
using System.Runtime.InteropServices;

namespace SSP
{
    internal class MsgSender
    {
        const int
            WM_LBUTTONDOWN = 513,
            WM_LBUTTONUP = 514,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_KEYDOWN = 256,
            WM_CHAR = 258,
            WM_KEYUP = 257,
            WM_SETFOCUS = 7,
            WM_SYSCOMMAND = 274,
            WM_GETTEXT = 0x000D,
            WM_GETTEXTLENGTH = 0x000E,
            WM_CLEAR = 0x303,
            WM_PAINT = 15,
            WM_SETCURSOR = 32,
            WM_KILLFOCUS = 8,
            WM_NCHITTEST = 132,
            WM_USER = 1024,
            WM_MOUSEACTIVATE = 33,
            WM_MOUSEMOVE = 512,
            WM_LBUTTONDBLCLK = 515,
            WM_COMMAND = 273,
            VK_DOWN = 0x28,
            VK_RETURN = 0x0D,
            BM_SETSTATE = 243,
            BM_CLICK = 0x00F5,
            SW_HIDE = 0,
            SW_MAXIMIZE = 3,
            SW_MINIMIZE = 6,
            SW_RESTORE = 9,
            SW_SHOW = 5,
            SW_SHOWDEFAULT = 10,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOWNORMAL = 1,
            SC_MINIMIZE = 32,
            EM_SETSEL = 0x00B1,
            CAPACITY = 256,
            CB_SETCURSEL = 0x014E;

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        internal static void MinimizeWindowByTitle(string title)  
        {
            IntPtr wnd = FindWindow(null, title);
            ShowWindow(wnd, WM_KILLFOCUS);
            ShowWindow(wnd, SW_MINIMIZE);
        }
    }
}
