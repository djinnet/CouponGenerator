using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Avalonia.Styling;

namespace CouponSheetGenerator.Desktop;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDesktopDialogs dialogs;
    private readonly ICouponPipeline pipeline;
    private readonly IDesktopPreferenceStore preferenceStore;
    private readonly DesktopPreferences preferences;
    private CancellationTokenSource? active;
    private ReadResult? imported;
    private Options settings = new();
    private string? previewPath;
    private int previewPages;
    private int currentPage;
    private bool previewHasBack;
    private int eligibleCount;
    public ObservableCollection<string> Issues { get; } = [];
    public ObservableCollection<string> VisibleIssues { get; } = [];
    public ObservableCollection<RecordSummary> VisibleRecords { get; } = [];
    public string Version => typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "Unknown";
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    public IReadOnlyList<PageSizeKind> PageSizeOptions { get; } = Enum.GetValues<PageSizeKind>();
    public IReadOnlyList<PageOrientation> OrientationOptions { get; } = Enum.GetValues<PageOrientation>();
    public IReadOnlyList<CardImageFit> ImageFitOptions { get; } = Enum.GetValues<CardImageFit>();
    public IReadOnlyList<string> UrlTextOptions { get; } = ["none", "short", "full"];
    [ObservableProperty] private string? inputPath;
    [ObservableProperty] private string summary = "Choose a TSV file to begin.";
    [ObservableProperty] private string layoutSummary = "Import records to calculate the layout.";
    [ObservableProperty] private string settingsError = "";
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string previewStatus = "No preview generated.";
    [ObservableProperty] private Bitmap? previewImage;
    public string PageLabel => previewPages == 0 ? "No pages" : $"Page {currentPage + 1} of {previewPages}" + (previewHasBack ? currentPage % 2 == 0 ? " · Front" : " · Back" : "");
    public double PreviewWidth => PreviewImage is null ? 600 : PreviewImage.PixelSize.Width * preferences.PreviewZoom;
    public string Theme
    {
        get => preferences.Theme;
        set
        {
            if (value is not ("System" or "Light" or "Dark")) return;
            preferences.Theme = value;
            ApplyTheme();
            SavePreferences();
            OnPropertyChanged();
        }
    }
    public string ZoomLabel => $"{preferences.PreviewZoom:P0}";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string recordFilter = "";
    [ObservableProperty] private string issueFilter = "";
    partial void OnRecordFilterChanged(string value) => RefreshRecords();
    partial void OnIssueFilterChanged(string value) => RefreshIssues();

    public MainViewModel(IDesktopDialogs dialogs, ICouponPipeline pipeline, IDesktopPreferenceStore preferenceStore)
    {
        this.dialogs = dialogs;
        this.pipeline = pipeline;
        this.preferenceStore = preferenceStore;
        preferences = preferenceStore.Load();
        if (preferences.PreviewZoom is < 0.25 or > 3) preferences.PreviewZoom = 1;
        ApplyTheme();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Title) or nameof(Footer) or nameof(PageSize) or nameof(Orientation) or nameof(Columns) or nameof(Rows) or nameof(Margin) or nameof(CardGap) or nameof(CutMarks) or nameof(PageNumbers) or nameof(IncludeGivenTo) or nameof(IncludeIds) or nameof(IncludeUrl) or nameof(IncludeRedeemed) or nameof(IncludeUnavailable) or nameof(IncludeExpired) or nameof(IncludeFuture) or nameof(BlackAndWhite) or nameof(AccentColor) or nameof(BackgroundColor) or nameof(ShowProductName) or nameof(ShowDates) or nameof(ShowStatus) or nameof(ShowOrderName) or nameof(InstructionText) or nameof(CodeFontSize) or nameof(QrSizeMm) or nameof(CardPaddingMm) or nameof(Logo)
                or nameof(SelectedPageSize) or nameof(SelectedOrientation) or nameof(LogoWidthMm) or nameof(LogoHeightMm) or nameof(BorderColor) or nameof(BorderWidthPt) or nameof(CornerRadiusMm) or nameof(CardBackgroundImage) or nameof(CardBackgroundImageFit) or nameof(BackEnabled) or nameof(BackTitle) or nameof(BackText) or nameof(BackBackgroundColor) or nameof(BackAccentColor) or nameof(BackBorderColor) or nameof(BackBackgroundImage) or nameof(BackBackgroundImageFit) or nameof(BackShowLogo) or nameof(BackMirrorColumns))
            {
                PreviewStatus = "Preview is stale. Refresh before reviewing.";
                UpdateLayoutSummary();
            }
        };
    }

    public string? Title { get => settings.Title; set { settings.Title = value; OnPropertyChanged(); } }
    public string? Footer { get => settings.Footer; set { settings.Footer = value; OnPropertyChanged(); } }
    public string PageSize { get => settings.PageSize; set { settings.PageSize = value; OnPropertyChanged(); } }
    public string Orientation { get => settings.Orientation; set { settings.Orientation = value; OnPropertyChanged(); } }
    public PageSizeKind SelectedPageSize { get => settings.PageSizeValue; set { settings.PageSizeValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageSize)); } }
    public PageOrientation SelectedOrientation { get => settings.OrientationValue; set { settings.OrientationValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(Orientation)); } }
    public int Columns { get => settings.Columns; set { settings.Columns = value; OnPropertyChanged(); } }
    public int Rows { get => settings.Rows; set { settings.Rows = value; OnPropertyChanged(); } }
    public double Margin { get => settings.Margin; set { settings.Margin = value; OnPropertyChanged(); } }
    public double CardGap { get => settings.CardGap; set { settings.CardGap = value; OnPropertyChanged(); } }
    public bool CutMarks { get => settings.CutMarks; set { settings.CutMarks = value; OnPropertyChanged(); } }
    public bool PageNumbers { get => settings.PageNumbers; set { settings.PageNumbers = value; OnPropertyChanged(); } }
    public bool IncludeGivenTo { get => settings.IncludeGivenTo; set { settings.IncludeGivenTo = value; OnPropertyChanged(); } }
    public bool IncludeIds { get => settings.IncludeIds; set { settings.IncludeIds = value; OnPropertyChanged(); } }
    public bool BlackAndWhite { get => settings.BlackAndWhite; set { settings.BlackAndWhite = value; OnPropertyChanged(); } }
    public string IncludeUrl { get => settings.IncludeUrl; set { settings.IncludeUrl = value; OnPropertyChanged(); } }
    public bool IncludeRedeemed { get => settings.IncludeRedeemed; set { settings.IncludeRedeemed = value; OnPropertyChanged(); RefreshRecords(); } }
    public bool IncludeUnavailable { get => settings.IncludeUnavailable; set { settings.IncludeUnavailable = value; OnPropertyChanged(); RefreshRecords(); } }
    public bool IncludeExpired { get => settings.IncludeExpired; set { settings.IncludeExpired = value; OnPropertyChanged(); RefreshRecords(); } }
    public bool IncludeFuture { get => settings.IncludeFuture; set { settings.IncludeFuture = value; OnPropertyChanged(); RefreshRecords(); } }
    public string AccentColor { get => settings.AccentColor; set { settings.AccentColor = value; OnPropertyChanged(); } }
    public string BackgroundColor { get => settings.BackgroundColor; set { settings.BackgroundColor = value; OnPropertyChanged(); } }
    public bool ShowProductName { get => settings.ShowProductName; set { settings.ShowProductName = value; OnPropertyChanged(); } }
    public bool ShowDates { get => settings.ShowDates; set { settings.ShowDates = value; OnPropertyChanged(); } }
    public bool ShowStatus { get => settings.ShowStatus; set { settings.ShowStatus = value; OnPropertyChanged(); } }
    public bool ShowOrderName { get => settings.ShowOrderName; set { settings.ShowOrderName = value; OnPropertyChanged(); } }
    public string InstructionText { get => settings.InstructionText; set { settings.InstructionText = value; OnPropertyChanged(); } }
    public double CodeFontSize { get => settings.CodeFontSize; set { settings.CodeFontSize = value; OnPropertyChanged(); } }
    public double QrSizeMm { get => settings.QrSizeMm; set { settings.QrSizeMm = value; OnPropertyChanged(); } }
    public double CardPaddingMm { get => settings.CardPaddingMm; set { settings.CardPaddingMm = value; OnPropertyChanged(); } }
    public string? Logo { get => settings.Logo; set { settings.Logo = value; OnPropertyChanged(); } }
    public double LogoWidthMm { get => settings.LogoWidthMm; set { settings.LogoWidthMm = value; OnPropertyChanged(); } }
    public double LogoHeightMm { get => settings.LogoHeightMm; set { settings.LogoHeightMm = value; OnPropertyChanged(); } }
    public string BorderColor { get => settings.BorderColor; set { settings.BorderColor = value; OnPropertyChanged(); } }
    public double BorderWidthPt { get => settings.BorderWidthPt; set { settings.BorderWidthPt = value; OnPropertyChanged(); } }
    public double CornerRadiusMm { get => settings.CornerRadiusMm; set { settings.CornerRadiusMm = value; OnPropertyChanged(); } }
    public string? CardBackgroundImage { get => settings.CardBackgroundImage; set { settings.CardBackgroundImage = value; OnPropertyChanged(); } }
    public CardImageFit CardBackgroundImageFit { get => settings.CardBackgroundImageFit; set { settings.CardBackgroundImageFit = value; OnPropertyChanged(); } }
    public bool BackEnabled { get => settings.BackEnabled; set { settings.BackEnabled = value; OnPropertyChanged(); } }
    public string BackTitle { get => settings.BackTitle; set { settings.BackTitle = value; OnPropertyChanged(); } }
    public string BackText { get => settings.BackText; set { settings.BackText = value; OnPropertyChanged(); } }
    public string BackBackgroundColor { get => settings.BackBackgroundColor; set { settings.BackBackgroundColor = value; OnPropertyChanged(); } }
    public string BackAccentColor { get => settings.BackAccentColor; set { settings.BackAccentColor = value; OnPropertyChanged(); } }
    public string BackBorderColor { get => settings.BackBorderColor; set { settings.BackBorderColor = value; OnPropertyChanged(); } }
    public string? BackBackgroundImage { get => settings.BackBackgroundImage; set { settings.BackBackgroundImage = value; OnPropertyChanged(); } }
    public CardImageFit BackBackgroundImageFit { get => settings.BackBackgroundImageFit; set { settings.BackBackgroundImageFit = value; OnPropertyChanged(); } }
    public bool BackShowLogo { get => settings.BackShowLogo; set { settings.BackShowLogo = value; OnPropertyChanged(); } }
    public bool BackMirrorColumns { get => settings.BackMirrorColumns; set { settings.BackMirrorColumns = value; OnPropertyChanged(); } }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task ChooseLogoAsync()
    {
        var path = await dialogs.OpenAsync("Choose local PNG or JPEG logo");
        if (path is null)
        {
            return;
        }

        Logo = path;
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task ChooseCardBackgroundAsync()
    {
        var path = await dialogs.OpenAsync("Choose front card PNG or JPEG background");
        if (path is not null) CardBackgroundImage = path;
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task ChooseBackBackgroundAsync()
    {
        var path = await dialogs.OpenAsync("Choose back card PNG or JPEG background");
        if (path is not null) BackBackgroundImage = path;
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void ClearCardBackground() => CardBackgroundImage = null;
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void ClearBackBackground() => BackBackgroundImage = null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void MinimalPreset()
    {
        ShowProductName = true;
        ShowDates = false;
        ShowStatus = false;
        ShowOrderName = false;
        InstructionText = "";
        IncludeUrl = "none";
        BlackAndWhite = false;
        Status = "Minimal preset applied.";
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void ModernPreset()
    {
        ShowProductName = true;
        ShowDates = true;
        ShowStatus = true;
        ShowOrderName = false;
        InstructionText = "Scan to redeem";
        IncludeUrl = "none";
        BlackAndWhite = false;
        AccentColor = "#183153";
        BackgroundColor = "#FFFFFF";
        Status = "Modern preset applied.";
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void InkSavingPreset()
    {
        ShowProductName = true;
        ShowDates = true;
        ShowStatus = false;
        ShowOrderName = false;
        InstructionText = "Scan to redeem";
        IncludeUrl = "none";
        BlackAndWhite = true;
        Status = "Ink-saving preset applied.";
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task ImportAsync()
    {
        var path = await dialogs.OpenAsync("Import TSV");
        if (path is null) return;
        await ImportPathAsync(path);
    }

    public async Task ImportPathAsync(string path)
    {
        await RunAsync(async token =>
        {
            var result = await Task.Run(() => { token.ThrowIfCancellationRequested(); return pipeline.Read(path); }, token);
            token.ThrowIfCancellationRequested();
            imported = result;
            InputPath = path;
            RefreshRecords();
            Issues.Clear();
            foreach (var warning in result.Warnings) Issues.Add(warning);
            RefreshIssues();
            var selection = pipeline.Select(result.Records, settings, DateOnly.FromDateTime(DateTime.Today));
            Summary = $"Imported {result.RowsRead}; valid {result.Records.Count}; invalid {result.InvalidRows}; eligible {selection.Coupons.Count}; excluded {result.RowsRead - selection.Coupons.Count}.";
            PreviewStatus = "Preview is stale. Refresh before reviewing.";
            Status = "Import completed.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task PreviewAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CouponSheetGenerator-preview-{Guid.NewGuid():N}.pdf");
        await GenerateAsync(path, true);
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task ExportAsync()
    {
        var path = await dialogs.SaveAsync("Export PDF", "pdf");
        if (path is null) return;
        await GenerateAsync(path, false);
    }

    private async Task GenerateAsync(string path, bool preview)
    {
        await RunAsync(async token =>
        {
            if (imported is null) return;
            ValidateSettings();
            var selection = pipeline.Select(imported.Records.ToArray(), settings, DateOnly.FromDateTime(DateTime.Today));
            if (selection.Coupons.Count == 0) throw new InvalidOperationException("No eligible coupons found.");
            var snapshot = CloneSettings();
            var temporary = preview ? path : Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            snapshot.Output = temporary;
            try
            {
                Status = preview ? "Generating preview…" : "Exporting PDF…";
                var pages = await Task.Run(() => { token.ThrowIfCancellationRequested(); return pipeline.WritePdf(selection.Coupons, snapshot); }, token);
                token.ThrowIfCancellationRequested();
                if (!preview) File.Move(temporary, path, true);
                if (preview)
                {
                    if (previewPath is { } old && File.Exists(old)) File.Delete(old);
                    previewPath = path; previewPages = pages; currentPage = 0; previewHasBack = snapshot.BackEnabled;
                    await RenderPageAsync(token);
                }
                PreviewStatus = preview ? $"Preview ready: {pages} pages, {selection.Coupons.Count} coupons." : "Export completed. Refresh preview after settings changes.";
                Status = preview ? "Preview PDF generated." : $"Exported {selection.Coupons.Count} coupons on {pages} pages.";
            }
            finally
            {
                if ((!preview || previewPath != path) && File.Exists(temporary)) File.Delete(temporary);
            }
        });
    }

    [RelayCommand]
    private void Cancel() => active?.Cancel();

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (currentPage > 0)
        {
            currentPage--;
            await RenderPageAsync(CancellationToken.None);
        }
    }
    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (currentPage + 1 < previewPages)
        {
            currentPage++;
            await RenderPageAsync(CancellationToken.None);
        }
    }
    private async Task RenderPageAsync(CancellationToken token)
    {
        if (previewPath is null) return;
        var path = previewPath;
        var page = currentPage;
        var bytes = await Task.Run(() =>
        {
            return RenderPdfPageToPng(path, page, token);
        }, token);
        token.ThrowIfCancellationRequested();
        using var imageStream = new MemoryStream(bytes);
        var image = new Bitmap(imageStream);
        var previous = PreviewImage;
        PreviewImage = image;
        previous?.Dispose();
        OnPropertyChanged(nameof(PageLabel));
        OnPropertyChanged(nameof(PreviewWidth));
    }

    private static byte[] RenderPdfPageToPng(string path, int page, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(path);
        using var bitmap = PDFtoImage.Conversion.ToImage(stream, page: page);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }

    [RelayCommand]
    private void ZoomIn() => SetZoom(Math.Min(3, preferences.PreviewZoom * 1.25));
    [RelayCommand]
    private void ZoomOut() => SetZoom(Math.Max(0.25, preferences.PreviewZoom / 1.25));
    [RelayCommand]
    private void FitPage() => SetZoom(1);
    private void SetZoom(double value)
    {
        preferences.PreviewZoom = value;
        OnPropertyChanged(nameof(ZoomLabel));
        OnPropertyChanged(nameof(PreviewWidth));
        SavePreferences();
    }
    private void ApplyTheme()
    {
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = preferences.Theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
    }
    private void SavePreferences()
    {
        try { preferenceStore.Save(preferences); }
        catch (IOException) { Status = "Could not save desktop preferences."; }
        catch (UnauthorizedAccessException) { Status = "Could not save desktop preferences."; }
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void ResetDesktopPreferences()
    {
        Theme = "System";
        SetZoom(1);
        Status = "Desktop preferences reset.";
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task LoadSettingsAsync()
    {
        var path = await dialogs.OpenAsync("Import shared settings JSON");
        if (path is null)
        {
            return;
        }

        await RunAsync(async token =>
        {
            var json = await File.ReadAllTextAsync(path, token);
            settings = JsonSerializer.Deserialize<Options>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            NotifySettings();
            Status = "Shared settings imported. Changes remain in this session until Save As.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task SaveSettingsAsync()
    {
        var path = await dialogs.SaveAsync("Save shared settings JSON", "json");
        if (path is null)
        {
            return;
        }

        await RunAsync(async token =>
        {
            ValidateSettings();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), token);
            Status = "Shared settings saved.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void ResetSettings()
    {
        settings = new();
        NotifySettings();
        Status = "Document settings reset to defaults.";
    }

    private Options CloneSettings() => JsonSerializer.Deserialize<Options>(JsonSerializer.Serialize(settings))!;
    private void RefreshRecords()
    {
        VisibleRecords.Clear();
        if (imported is null)
        {
            return;
        }

        var selected = pipeline.Select(imported.Records, settings, DateOnly.FromDateTime(DateTime.Today));
        eligibleCount = selected.Coupons.Count;
        Summary = $"Imported {imported.RowsRead}; valid {imported.Records.Count}; invalid {imported.InvalidRows}; eligible {selected.Coupons.Count}; excluded {imported.RowsRead - selected.Coupons.Count}.";
        UpdateLayoutSummary();
        var eligibleRows = selected.Coupons.Select(r => r.Row).ToHashSet();
        foreach (var record in imported.Records.Where(r => r.Product.Contains(RecordFilter, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleRecords.Add(new RecordSummary(record.Row, record.Product, eligibleRows.Contains(record.Row) ? "Eligible" : "Excluded"));
        }
    }
    private void UpdateLayoutSummary()
    {
        try
        {
            ValidateSettings();
            SettingsError = "";
        }
        catch (ArgumentException ex)
        {
            SettingsError = ex.Message;
            LayoutSummary = ex.Message;
            PreviewCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SettingsError = "Could not read a selected image file.";
            LayoutSummary = SettingsError;
            PreviewCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            return;
        }
        if (imported is null)
        {
            return;
        }

        try
        {
            var count = pipeline.Select(imported.Records, settings, DateOnly.FromDateTime(DateTime.Today)).Coupons.Count;
            var layout = CouponLayout.Calculate(settings, count);
            LayoutSummary = $"{count} coupons · {layout.PageCount} pages · cards {layout.CardWidthMm:F1} × {layout.CardHeightMm:F1} mm";
        }
        catch (ArgumentException ex) { LayoutSummary = ex.Message; }
        PreviewCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged();
    }
    private bool CanStart() => !IsBusy;
    private bool CanGenerate() => !IsBusy && eligibleCount > 0 && SettingsError.Length == 0;
    private void NotifySettings()
    {
        var names = new[] {
            nameof(Title),
            nameof(Footer),
            nameof(PageSize),
            nameof(Orientation),
            nameof(Columns),
            nameof(Rows),
            nameof(Margin),
            nameof(CardGap),
            nameof(CutMarks),
            nameof(PageNumbers),
            nameof(IncludeGivenTo),
            nameof(IncludeIds),
            nameof(IncludeUrl),
            nameof(IncludeRedeemed),
            nameof(IncludeUnavailable),
            nameof(IncludeExpired),
            nameof(IncludeFuture),
            nameof(BlackAndWhite),
            nameof(AccentColor),
            nameof(BackgroundColor),
            nameof(ShowProductName),
            nameof(ShowDates),
            nameof(ShowStatus),
            nameof(ShowOrderName),
            nameof(InstructionText),
            nameof(CodeFontSize),
            nameof(QrSizeMm),
            nameof(CardPaddingMm),
            nameof(Logo), nameof(SelectedPageSize), nameof(SelectedOrientation), nameof(LogoWidthMm), nameof(LogoHeightMm),
            nameof(BorderColor), nameof(BorderWidthPt), nameof(CornerRadiusMm), nameof(CardBackgroundImage), nameof(CardBackgroundImageFit),
            nameof(BackEnabled), nameof(BackTitle), nameof(BackText), nameof(BackBackgroundColor), nameof(BackAccentColor),
            nameof(BackBorderColor), nameof(BackBackgroundImage), nameof(BackBackgroundImageFit), nameof(BackShowLogo), nameof(BackMirrorColumns)
        };

        foreach (var name in names)
        {
            OnPropertyChanged(name);
        }

        RefreshRecords();
    }
    private void ValidateSettings()
    {
        if (IncludeUrl is not ("none" or "short" or "full")) throw new ArgumentException("Choose none, short, or full URL text.");
        CouponLayout.Calculate(settings, eligibleCount);
        if (!string.IsNullOrWhiteSpace(Logo)) LocalImage.Validate(Logo);
        if (!string.IsNullOrWhiteSpace(CardBackgroundImage)) LocalImage.Validate(CardBackgroundImage);
        if (BackEnabled && !string.IsNullOrWhiteSpace(BackBackgroundImage)) LocalImage.Validate(BackBackgroundImage);
    }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (IsBusy)
        {
            return;
        }

        using var source = new CancellationTokenSource();
        active = source;
        IsBusy = true;
        ImportCommand.NotifyCanExecuteChanged(); PreviewCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged();
        LoadSettingsCommand.NotifyCanExecuteChanged(); SaveSettingsCommand.NotifyCanExecuteChanged(); ResetSettingsCommand.NotifyCanExecuteChanged();
        ResetDesktopPreferencesCommand.NotifyCanExecuteChanged();
        MinimalPresetCommand.NotifyCanExecuteChanged(); ModernPresetCommand.NotifyCanExecuteChanged(); InkSavingPresetCommand.NotifyCanExecuteChanged();
        ChooseLogoCommand.NotifyCanExecuteChanged();
        ChooseCardBackgroundCommand.NotifyCanExecuteChanged(); ChooseBackBackgroundCommand.NotifyCanExecuteChanged();
        ClearCardBackgroundCommand.NotifyCanExecuteChanged(); ClearBackBackgroundCommand.NotifyCanExecuteChanged();
        try { await work(source.Token); }
        catch (OperationCanceledException) { Status = "Cancelled."; }
        catch (Exception ex) { Status = $"Error: {ex.GetType().Name}. Check the file and settings."; Issues.Add(Status); RefreshIssues(); }
        finally
        {
            active = null;
            IsBusy = false;
            ImportCommand.NotifyCanExecuteChanged(); PreviewCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged();
            LoadSettingsCommand.NotifyCanExecuteChanged(); SaveSettingsCommand.NotifyCanExecuteChanged(); ResetSettingsCommand.NotifyCanExecuteChanged();
            ResetDesktopPreferencesCommand.NotifyCanExecuteChanged();
            MinimalPresetCommand.NotifyCanExecuteChanged(); ModernPresetCommand.NotifyCanExecuteChanged(); InkSavingPresetCommand.NotifyCanExecuteChanged();
            ChooseLogoCommand.NotifyCanExecuteChanged();
            ChooseCardBackgroundCommand.NotifyCanExecuteChanged(); ChooseBackBackgroundCommand.NotifyCanExecuteChanged();
            ClearCardBackgroundCommand.NotifyCanExecuteChanged(); ClearBackBackgroundCommand.NotifyCanExecuteChanged();
        }
    }
    private void RefreshIssues()
    {
        VisibleIssues.Clear();
        foreach (var issue in Issues.Where(i => i.Contains(IssueFilter, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleIssues.Add(issue);
        }
    }
    public void Dispose()
    {
        active?.Cancel();
        PreviewImage?.Dispose();
        if (previewPath is { } path && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

public sealed record RecordSummary(int Row, string Product, string Status);
