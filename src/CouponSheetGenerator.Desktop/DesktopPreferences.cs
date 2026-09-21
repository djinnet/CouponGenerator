using System.Text.Json;

namespace CouponSheetGenerator.Desktop;

public sealed class DesktopPreferences
{
    public string Theme { get; set; } = "System";
    public double PreviewZoom { get; set; } = 1;
}

public interface IDesktopPreferenceStore
{
    DesktopPreferences Load();
    void Save(DesktopPreferences preferences);
}

public sealed class DesktopPreferenceStore : IDesktopPreferenceStore
{
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CouponSheetGenerator", "desktop-preferences.json");

    public DesktopPreferences Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<DesktopPreferences>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
        catch (JsonException) { return new(); }
    }

    public void Save(DesktopPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
