using CouponSheetGenerator.Desktop;

namespace CouponSheetGenerator.Tests;

public class DesktopTests
{
    private sealed class Dialogs(string? input) : IDesktopDialogs
    {
        public Task<string?> OpenAsync(string title) => Task.FromResult(input);
        public Task<string?> SaveAsync(string title, string extension) => Task.FromResult<string?>(null);
    }
    private sealed class Preferences : IDesktopPreferenceStore
    {
        public DesktopPreferences Load() => new();
        public void Save(DesktopPreferences preferences) { }
    }

    [Fact]
    public async Task ImportShowsSummaryAndFiltersRecordsWithoutChangingExportCount()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "Product name\tPromotional code\tRedeemable URL\nCoffee\t0001\thttps://example.invalid/1\nTea\t0002\thttps://example.invalid/2\n");
            using var vm = new MainViewModel(new Dialogs(path), new CouponPipeline(), new Preferences());
            await vm.ImportCommand.ExecuteAsync(null);
            Assert.Contains("eligible 2", vm.Summary);
            Assert.Equal(2, vm.VisibleRecords.Count);
            vm.RecordFilter = "Tea";
            Assert.Single(vm.VisibleRecords);
            Assert.Contains("eligible 2", vm.Summary);
            Assert.True(vm.ExportCommand.CanExecute(null));
            vm.Title = "New title";
            Assert.Contains("stale", vm.PreviewStatus);
            vm.Columns = 0;
            Assert.NotEmpty(vm.SettingsError);
            Assert.False(vm.ExportCommand.CanExecute(null));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DuplicateCodesAreRetainedAndReportedWithoutValues()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "Product name\tPromotional code\tRedeemable URL\nA\tSECRET-001\thttps://example.invalid/1\nB\tSECRET-001\thttps://example.invalid/2\n");
            var result = TsvReader.Read(path);
            Assert.Equal(2, result.Records.Count);
            Assert.Contains(result.Warnings, w => w.Contains("duplicate promotional code"));
            Assert.DoesNotContain("SECRET-001", string.Join(" ", result.Warnings));
        }
        finally { File.Delete(path); }
    }
}
