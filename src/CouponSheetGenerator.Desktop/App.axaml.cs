using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CouponSheetGenerator;

namespace CouponSheetGenerator.Desktop;

public partial class App : Application
{
    private MainViewModel? viewModel;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            viewModel = new MainViewModel(new DesktopDialogs(window), new CouponPipeline(), new DesktopPreferenceStore());
            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => viewModel?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
