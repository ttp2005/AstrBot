using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DesktopShow;

public sealed class DesktopItem : INotifyPropertyChanged
{
    public static IReadOnlyList<string> Categories { get; } =
        new[] { "快捷方式", "文件夹", "文件" };

    private string _category;

    public DesktopItem(string path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A desktop item path is required.", nameof(path));
        }

        Path = path;
        DefaultCategory = GetDefaultCategory(path, isDirectory);
        _category = DefaultCategory;

        var name = System.IO.Path.GetFileName(path);
        Name = string.IsNullOrEmpty(name) ? path : name;
    }

    public string Path { get; }

    public string Name { get; }

    public string DefaultCategory { get; }

    public ImageSource? Icon { get; set; }

    public string Category
    {
        get => _category;
        set
        {
            if (!IsSupportedCategory(value))
            {
                throw new ArgumentException($"Unknown desktop category: {value}", nameof(value));
            }

            if (_category == value)
            {
                return;
            }

            _category = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string GetDefaultCategory(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return Categories[1];
        }

        return System.IO.Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            ? Categories[0]
            : Categories[2];
    }

    public static bool IsSupportedCategory(string? category) =>
        category is not null && Categories.Contains(category, StringComparer.Ordinal);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
