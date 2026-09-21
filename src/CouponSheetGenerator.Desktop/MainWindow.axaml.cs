using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace CouponSheetGenerator.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            await vm.ImportPathAsync(path);
        }
    }
    private void OpenPreviewWindow(object? sender, RoutedEventArgs e)
    {
        var window = new Window { Title = "Coupon preview", Width = 850, Height = 1000, Content = new ScrollViewer { Content = new Image { [!Image.SourceProperty] = new Avalonia.Data.Binding("PreviewImage"), Stretch = Avalonia.Media.Stretch.Uniform } }, DataContext = DataContext };
        window.Show(this);
    }
    private async void CopyDiagnostics(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync($"{vm.Summary}{Environment.NewLine}{string.Join(Environment.NewLine, vm.Issues)}");
        }
    }
}
