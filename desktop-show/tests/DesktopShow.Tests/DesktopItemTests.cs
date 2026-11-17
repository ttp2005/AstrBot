using Xunit;

namespace DesktopShow.Tests;

public sealed class DesktopItemTests
{
    [Theory]
    [InlineData(@"C:\Desktop\app.lnk", false, "快捷方式")]
    [InlineData(@"C:\Desktop\Documents", true, "文件夹")]
    [InlineData(@"C:\Desktop\notes.txt", false, "文件")]
    public void UsesExpectedDefaultCategory(string path, bool isDirectory, string expected)
    {
        var item = new DesktopItem(path, isDirectory);

        Assert.Equal(expected, item.Category);
    }

    [Fact]
    public void RejectsUnknownCategory()
    {
        var item = new DesktopItem(@"C:\Desktop\notes.txt", false);

        Assert.Throws<ArgumentException>(() => item.Category = "未知分组");
    }
}
