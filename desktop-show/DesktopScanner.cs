using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopShow;

public sealed class DesktopScanner
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    public IReadOnlyList<DesktopItem> Scan() =>
        Scan(new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        });

    public IReadOnlyList<DesktopItem> Scan(IEnumerable<string> desktopDirectories)
    {
        var items = new List<DesktopItem>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = desktopDirectories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories)
        {
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(directory);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or SecurityException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (string.Equals(Path.GetFileName(entry), "desktop.ini", StringComparison.OrdinalIgnoreCase)
                    || !seenPaths.Add(entry))
                {
                    continue;
                }

                try
                {
                    var attributes = File.GetAttributes(entry);
                    items.Add(new DesktopItem(
                        entry,
                        (attributes & FileAttributes.Directory) == FileAttributes.Directory));
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException
                    or SecurityException)
                {
                    // A single inaccessible or disappearing item must not hide the desktop.
                }
            }
        }

        return items
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static ImageSource? GetIcon(string path)
    {
        _ = SHGetFileInfo(
            path,
            0,
            out var fileInfo,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            ShgfiIcon | ShgfiLargeIcon);

        if (fileInfo.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var icon = Imaging.CreateBitmapSourceFromHIcon(
                fileInfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            icon.Freeze();
            return icon;
        }
        catch (ExternalException)
        {
            return null;
        }
        finally
        {
            _ = DestroyIcon(fileInfo.hIcon);
        }
    }

    public static void Open(DesktopItem item) =>
        Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
