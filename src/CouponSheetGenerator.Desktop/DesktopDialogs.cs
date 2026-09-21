using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CouponSheetGenerator.Desktop;

public interface IDesktopDialogs
{
    Task<string?> OpenAsync(string title);
    Task<string?> SaveAsync(string title, string extension);
}

public sealed class DesktopDialogs(Window window) : IDesktopDialogs
{
    public async Task<string?> OpenAsync(string title)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> SaveAsync(string title, string extension)
    {
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = extension,
            ShowOverwritePrompt = true
        });
        return file?.TryGetLocalPath();
    }
}
