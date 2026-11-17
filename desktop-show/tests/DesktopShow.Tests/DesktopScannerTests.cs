using System.IO;
using Xunit;

namespace DesktopShow.Tests;

public sealed class DesktopScannerTests
{
    [Fact]
    public void ScanDeduplicatesPathsAndSkipsDesktopIni()
    {
        var directory = Directory.CreateTempSubdirectory("DesktopShow.Tests.");
        try
        {
            var shortcut = Path.Combine(directory.FullName, "app.lnk");
            File.WriteAllText(shortcut, string.Empty);
            File.WriteAllText(Path.Combine(directory.FullName, "desktop.ini"), string.Empty);

            var items = new DesktopScanner().Scan(new[]
            {
                directory.FullName,
                directory.FullName.ToUpperInvariant(),
            });

            var item = Assert.Single(items);
            Assert.Equal(shortcut, item.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [Fact]
    public void ScanContinuesWhenAnotherDesktopDirectoryIsMissing()
    {
        var directory = Directory.CreateTempSubdirectory("DesktopShow.Tests.");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "notes.txt"), string.Empty);
            var missingDirectory = Path.Combine(directory.FullName, "missing");

            var items = new DesktopScanner().Scan(new[]
            {
                missingDirectory,
                directory.FullName,
            });

            var item = Assert.Single(items);
            Assert.Equal("notes.txt", item.Name);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [Fact]
    public void ScanIdentifiesFolders()
    {
        var directory = Directory.CreateTempSubdirectory("DesktopShow.Tests.");
        try
        {
            Directory.CreateDirectory(Path.Combine(directory.FullName, "Documents"));

            var item = Assert.Single(new DesktopScanner().Scan(new[] { directory.FullName }));

            Assert.Equal("文件夹", item.DefaultCategory);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }
}
