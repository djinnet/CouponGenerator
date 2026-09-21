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
        string path = Temp(Header + Row(url: "ftp://example.invalid/x") + Row(start: "02/03/2026", expiry: "nonsense"));
        var result = TsvReader.Read(path);
        Assert.Equal(1, result.InvalidRows);
        Assert.Single(result.Records);
        Assert.Null(result.Records[0].Start);
        Assert.Contains(result.Warnings, w => w.Contains("Row 3") && w.Contains("ambiguous"));
        File.Delete(path);
    }
    [Fact] public void HttpAndHttpsUrlsAreAccepted()
    {
        string path = Temp(Header + Row(code: "HTTP", url: "http://example.invalid/x") + Row(code: "HTTPS", url: "https://example.invalid/y"));
        try
        {
            var result = TsvReader.Read(path);
            Assert.Equal(0, result.InvalidRows);
            Assert.Equal(2, result.Records.Count);
        }
        finally { File.Delete(path); }
    }
    [Fact] public void MissingHeaderIsFatal()
    {
        string path = Temp("Product name\tPromotional code\nA\tB\n");
        Assert.Throws<InvalidOperationException>(() => TsvReader.Read(path));
        File.Delete(path);
    }
    [Fact] public void DuplicateHeadersAndWrongDelimiterAreFatal()
    {
        var duplicate = Temp("Product name\tPromotional code\t promotional CODE \tRedeemable URL\nA\tB\tC\thttps://example.invalid/\n");
        var csv = Temp("Product name,Promotional code,Redeemable URL\nA,B,https://example.invalid/\n");
        try
        {
            Assert.Contains("Duplicate TSV header", Assert.Throws<InvalidOperationException>(() => TsvReader.Read(duplicate)).Message);
            Assert.Contains("Missing required headers", Assert.Throws<InvalidOperationException>(() => TsvReader.Read(csv)).Message);
        }
        finally { File.Delete(duplicate); File.Delete(csv); }
    }
    [Fact] public void HeaderOnlyFileIsReported()
    {
        var path = Temp(Header);
        try { Assert.Contains(TsvReader.Read(path).Warnings, w => w.Contains("no records")); }
        finally { File.Delete(path); }
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
    [Fact] public void RenderedPdfQrDecodesToExactUrl()
    {
        const string url = "https://example.invalid/redeem/0001?token=%C3%A6&k=01";
        string output = Path.GetTempFileName();
        try
        {
            var records = new[] { new CouponRecord(2, "Coupon", "0001", url, null, null, true, false, "", "", "") };
            PdfWriter.Write(records, new Options { Output = output });
            using var stream = File.OpenRead(output);
            using var bitmap = PDFtoImage.Conversion.ToImage(stream, page: 0);
            byte[] pixels = new byte[bitmap.Width * bitmap.Height * 3];
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                int i = (y * bitmap.Width + x) * 3;
                pixels[i] = color.Red; pixels[i + 1] = color.Green; pixels[i + 2] = color.Blue;
            }
            var source = new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGB24);
            Assert.Equal(url, new BarcodeReaderGeneric().Decode(source)?.Text);
        }
        finally { File.Delete(output); }
    }
    [Fact] public void LayoutCalculationMatchesPaginationAndRejectsImpossibleGrid()
    {
        var layout = CouponLayout.Calculate(new Options(), 205);
        Assert.Equal(6, layout.CouponsPerPage);
        Assert.Equal(35, layout.PageCount);
        Assert.InRange(layout.CardWidthMm, 92, 94);
        Assert.Throws<ArgumentException>(() => CouponLayout.Calculate(new Options { Rows = 10, Columns = 10 }, 1));
    }
    [Fact] public void TwoSidedCustomCardRendersBothPages()
    {
        var imagePath = Path.GetTempFileName();
        var output = Path.GetTempFileName();
        try
        {
            using (var bitmap = new SKBitmap(64, 64))
            {
                bitmap.Erase(SKColors.LightBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(imagePath, data.ToArray());
            }
            var options = new Options
            {
                Output = output, BackEnabled = true, BackTitle = "Visit again", BackText = "Terms on reverse",
                Logo = imagePath, LogoWidthMm = 18, LogoHeightMm = 10,
                CardBackgroundImage = imagePath, BackBackgroundImage = imagePath,
                BorderColor = "#123456", BackBorderColor = "#654321", BorderWidthPt = 1.5, CornerRadiusMm = 2,
                PageSizeValue = PageSizeKind.Letter, OrientationValue = PageOrientation.Landscape
            };
            Assert.Equal(2, CouponLayout.Calculate(options, 1).PageCount);
            var records = new[] { new CouponRecord(2, "Coupon", "0001", "https://example.invalid/1", null, null, true, false, "", "", "") };
            Assert.Equal(2, PdfWriter.Write(records, options));
            using var frontStream = File.OpenRead(output);
            using var front = PDFtoImage.Conversion.ToImage(frontStream, page: 0);
            using var backStream = File.OpenRead(output);
            using var back = PDFtoImage.Conversion.ToImage(backStream, page: 1);
            Assert.Equal(front.Width, back.Width);
            Assert.Equal(front.Height, back.Height);
            int ColoredSamples(int startX, int endX)
            {
                int count = 0;
                for (int y = back.Height / 10; y < back.Height / 3; y += 12)
                for (int x = startX; x < endX; x += 12)
                {
                    var color = back.GetPixel(x, y);
                    if (color.Red < 245 || color.Green < 245 || color.Blue < 245) count++;
                }
                return count;
            }
            Assert.True(ColoredSamples(back.Width / 2, back.Width) > ColoredSamples(0, back.Width / 2));
        }
        finally { File.Delete(imagePath); File.Delete(output); }
    }
    [Fact] public void LegacyPageStringsAndEnumChoicesRoundTrip()
    {
        var options = System.Text.Json.JsonSerializer.Deserialize<Options>("{\"PageSize\":\"Letter\",\"Orientation\":\"landscape\",\"CardBackgroundImageFit\":\"Stretch\"}")!;
        Assert.Equal(PageSizeKind.Letter, options.PageSizeValue);
        Assert.Equal(PageOrientation.Landscape, options.OrientationValue);
        Assert.Equal(CardImageFit.Stretch, options.CardBackgroundImageFit);
        options.OrientationValue = PageOrientation.Portrait;
        var json = System.Text.Json.JsonSerializer.Serialize(options);
        Assert.Contains("\"PageSize\":\"Letter\"", json);
        Assert.Contains("\"Orientation\":\"portrait\"", json);
        Assert.DoesNotContain("PageSizeValue", json);
    }
}
