using System.IO;
using Xunit;

namespace DesktopShow.Tests;

public sealed class CategoryStoreTests
{
    [Fact]
    public void LoadReturnsEmptyCollectionForMissingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "categories.json");

        var categories = new CategoryStore(path).Load();

        Assert.Empty(categories);
    }

    [Fact]
    public void SaveAndLoadRoundTripUsesCaseInsensitivePaths()
    {
        var directory = Directory.CreateTempSubdirectory("DesktopShow.Tests.");
        try
        {
            var store = new CategoryStore(Path.Combine(directory.FullName, "categories.json"));
            var shortcut = new DesktopItem(@"C:\Desktop\app.lnk", false)
            {
                Category = "文件夹",
            };
            var document = new DesktopItem(@"C:\Desktop\notes.txt", false);

            store.Save(new[] { shortcut, document });
            var categories = store.Load();

            Assert.Equal(2, categories.Count);
            Assert.Equal("文件夹", categories[@"c:\desktop\APP.LNK"]);
            Assert.Equal("文件", categories[document.Path]);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [Fact]
    public void LoadFallsBackToDefaultsForCorruptedJson()
    {
        var directory = Directory.CreateTempSubdirectory("DesktopShow.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, "categories.json");
            File.WriteAllText(path, "not valid json");

            var categories = new CategoryStore(path).Load();

            Assert.Empty(categories);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [Fact]
    public void SaveRemovesEntriesForItemsThatNoLongerExist()
    {
        var directory = Directory.CreateTempSubdirectory("DesktopShow.Tests.");
        try
        {
            var store = new CategoryStore(Path.Combine(directory.FullName, "categories.json"));
            var removed = new DesktopItem(@"C:\Desktop\removed.lnk", false);
            var retained = new DesktopItem(@"C:\Desktop\retained.txt", false);
            store.Save(new[] { removed, retained });

            store.Save(new[] { retained });
            var categories = store.Load();

            var category = Assert.Single(categories);
            Assert.Equal(retained.Path, category.Key);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }
}
