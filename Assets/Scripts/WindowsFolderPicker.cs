using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Minimal native folder picker for Windows player/editor builds.
/// </summary>
public static class WindowsFolderPicker
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    delegate int BrowseCallbackProc(IntPtr hwnd, uint msg, IntPtr lParam, IntPtr lpData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszTitle;
        public uint ulFlags;
        public BrowseCallbackProc lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    const uint BifReturnOnlyFsDirs = 0x0001;
    const uint BifEditBox = 0x0010;
    const uint BifNewDialogStyle = 0x0040;
    const uint BifShareable = 0x8000;

    const uint BffmSetSelectionW = 0x467;
    const uint BffmInitialized = 1;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHBrowseForFolder(ref BROWSEINFO browseInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder path);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, string lParam);

    [DllImport("ole32.dll")]
    static extern void CoTaskMemFree(IntPtr ptr);

    public static bool TryPickFolder(string title, string initialPath, out string folderPath)
    {
        folderPath = "";

        BrowseCallbackProc callback = (hwnd, msg, lParam, lpData) =>
        {
            if (msg == BffmInitialized && !string.IsNullOrWhiteSpace(initialPath))
            {
                SendMessage(hwnd, BffmSetSelectionW, new IntPtr(1), initialPath);
            }

            return 0;
        };

        var browseInfo = new BROWSEINFO
        {
            lpszTitle = title,
            ulFlags = BifReturnOnlyFsDirs | BifEditBox | BifNewDialogStyle | BifShareable,
            lpfn = callback,
        };

        var pidl = SHBrowseForFolder(ref browseInfo);
        if (pidl == IntPtr.Zero)
        {
            GC.KeepAlive(callback);
            return false;
        }

        try
        {
            var builder = new StringBuilder(1024);
            if (!SHGetPathFromIDList(pidl, builder))
            {
                return false;
            }

            folderPath = builder.ToString();
            return !string.IsNullOrWhiteSpace(folderPath);
        }
        finally
        {
            CoTaskMemFree(pidl);
            GC.KeepAlive(callback);
        }
    }
#else
    public static bool TryPickFolder(string title, string initialPath, out string folderPath)
    {
        folderPath = "";
        return false;
    }
#endif
}
