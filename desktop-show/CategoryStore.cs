using System.IO;
using System.Text.Json;

namespace DesktopShow;

public sealed class CategoryStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public CategoryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopShow",
            "categories.json"))
    {
    }

    public CategoryStore(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
    }

    public Dictionary<string, string> Load()
    {
        var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(_filePath))
            {
                return categories;
            }

            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_filePath));
            if (saved is null)
            {
                return categories;
            }

            foreach (var entry in saved)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key)
                    && DesktopItem.IsSupportedCategory(entry.Value))
                {
                    categories[entry.Key] = entry.Value;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return categories;
    }

    public void Save(IEnumerable<DesktopItem> items)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The category file must have a parent directory.");
        Directory.CreateDirectory(directory);

        var categories = items.ToDictionary(
            item => item.Path,
            item => item.Category,
            StringComparer.OrdinalIgnoreCase);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(categories, _jsonOptions));

        // Replace the configuration only after the complete JSON document is written.
        File.Move(temporaryPath, _filePath, true);
    }
}
