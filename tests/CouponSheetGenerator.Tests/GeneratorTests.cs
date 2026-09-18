using CouponSheetGenerator;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace CouponSheetGenerator.Tests;

public class GeneratorTests
{
    static string Temp(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        return path;
    }
    const string Header = "Product name\tOrder name\tPromotional code\tRedeemable URL\tStart date\tExpire date\tCode ID\tOrder ID\tGiven to\tAvailable\tRedeemed\n";
    static string Row(string product = "Café", string code = "0001-AB", string url = "https://example.invalid/redeem/1", string start = "", string expiry = "", string available = "yes", string redeemed = "no")
        => $"{product}\t\t{code}\t{url}\t{start}\t{expiry}\t\t\t\t{available}\t{redeemed}\n";

    [Fact] public void HeadersBomUnicodeAndLeadingZeros()
    {
        string path = Temp(" redeemable url \t promotional CODE \t PRODUCT NAME \tExtra\nhttps://example.invalid/é\t0007-Ab\tBøger 東京\tx\n");
        var result = TsvReader.Read(path);
        Assert.Equal(1, result.RowsRead);
        Assert.Equal("0007-Ab", result.Records.Single().Code);
        Assert.Equal("Bøger 東京", result.Records.Single().Product);
        File.Delete(path);
    }
    [Fact] public void QuotedTabsQuotesAndNewlines()
    {
        string path = Temp(Header + "\"Coffee\t\"\"To Go\"\"\nSpecial\"\t\t0002\thttps://example.invalid/x\t\t\t\t\t\t1\t0\n");
        var record = TsvReader.Read(path).Records.Single();
        Assert.Contains("\t", record.Product);
        Assert.Contains("\n", record.Product);
        Assert.Equal("0002", record.Code);
        File.Delete(path);
    }
    [Fact] public void InvalidRowsAndDatesGiveRowWarnings()
    {
        string path = Temp(Header + Row(url: "http://example.invalid/x") + Row(start: "02/03/2026", expiry: "nonsense"));
        var result = TsvReader.Read(path);
        Assert.Equal(1, result.InvalidRows);
        Assert.Single(result.Records);
        Assert.Null(result.Records[0].Start);
        Assert.Contains(result.Warnings, w => w.Contains("Row 3") && w.Contains("ambiguous"));
        File.Delete(path);
    }
    [Fact] public void MissingHeaderIsFatal()
    {
        string path = Temp("Product name\tPromotional code\nA\tB\n");
        Assert.Throws<InvalidOperationException>(() => TsvReader.Read(path));
        File.Delete(path);
    }
    [Theory]
    [InlineData("true", "false", true, false)]
    [InlineData("YES", "NO", true, false)]
    [InlineData("1", "0", true, false)]
    [InlineData("no", "yes", false, true)]
    public void BooleanVariations(string available, string redeemed, bool expectedAvailable, bool expectedRedeemed)
    {
        string path = Temp(Header + Row(available: available, redeemed: redeemed));
        var record = TsvReader.Read(path).Records.Single();
        Assert.Equal(expectedAvailable, record.Available);
        Assert.Equal(expectedRedeemed, record.Redeemed);
        File.Delete(path);
    }
    [Fact] public void IsoDatesAndEmptyOptionalFields()
    {
        string path = Temp(Header + Row(start: "2026-09-18", expiry: "2028/12/31"));
        var record = TsvReader.Read(path).Records.Single();
        Assert.Equal(new DateOnly(2026, 9, 18), record.Start);
        Assert.Equal(new DateOnly(2028, 12, 31), record.Expiry);
        Assert.Equal("", record.GivenTo);
        File.Delete(path);
    }
    [Fact] public void LongProductAndPartialLastPage()
    {
        var records = Enumerable.Range(0, 7).Select(i => new CouponRecord(i+2, "東京 " + new string('X', 300), $"LONG-{i}", $"https://example.invalid/{i}", null, null, true, false, "", "", "")).ToList();
        string output = Path.GetTempFileName();
        Assert.Equal(2, PdfWriter.Write(records, new Options { Output = output }));
        Assert.True(new FileInfo(output).Length > 1000);
        File.Delete(output);
    }
    [Fact] public void FiltersAndOverrides()
    {
        var today = new DateOnly(2026, 9, 18);
        var records = new[]
        {
            new CouponRecord(2,"A","1","https://example.invalid/1",null,null,true,false,"","",""),
            new CouponRecord(3,"B","2","https://example.invalid/2",null,null,true,true,"","",""),
            new CouponRecord(4,"C","3","https://example.invalid/3",null,null,false,false,"","",""),
            new CouponRecord(5,"D","4","https://example.invalid/4",null,today.AddDays(-1),true,false,"","",""),
            new CouponRecord(6,"E","5","https://example.invalid/5",today.AddDays(1),null,true,false,"","","")
        };
        var selected = Eligibility.Select(records, new Options(), today);
        Assert.Single(selected.Coupons);
        Assert.Equal(1, selected.RedeemedExcluded);
        Assert.Equal(1, selected.UnavailableExcluded);
        Assert.Equal(1, selected.ExpiredExcluded);
        Assert.Equal(1, selected.FutureExcluded);
        Assert.Equal(5, Eligibility.Select(records, new Options { IncludeRedeemed=true, IncludeUnavailable=true, IncludeExpired=true, IncludeFuture=true }, today).Coupons.Count);
    }
    [Fact] public void OverTwoHundredRowsAndPdfPagination()
    {
        string path = Temp(Header + string.Concat(Enumerable.Range(0, 205).Select(i => Row(code: $"00{i}", url: $"https://example.invalid/{i}"))));
        var records = TsvReader.Read(path).Records;
        Assert.Equal(205, records.Count);
        string output = Path.GetTempFileName();
        Assert.Equal(35, PdfWriter.Write(records, new Options { Output = output }));
        Assert.True(new FileInfo(output).Length > 100_000);
        File.Delete(output); File.Delete(path);
    }
    [Fact] public void QrPngDecodesToExactUrl()
    {
        const string url = "https://example.invalid/redeem/0001?x=%C3%A6&y=1";
        using var bitmap = SKBitmap.Decode(PdfWriter.QrPng(url));
        byte[] pixels = new byte[bitmap.Width * bitmap.Height * 3];
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            int i = (y * bitmap.Width + x) * 3;
            pixels[i] = color.Red; pixels[i+1] = color.Green; pixels[i+2] = color.Blue;
        }
        var source = new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        var decoded = new BarcodeReaderGeneric().Decode(source);
        Assert.Equal(url, decoded?.Text);
    }
}
