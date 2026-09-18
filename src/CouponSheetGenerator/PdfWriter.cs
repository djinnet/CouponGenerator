using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CouponSheetGenerator;

public static class PdfWriter
{
    public static byte[] QrPng(string url)
    {
        using var qr = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(qr).GetGraphic(12, drawQuietZones: true);
    }

    public static int Write(IReadOnlyList<CouponRecord> coupons, Options o)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var page = o.PageSize == "Letter" ? PageSizes.Letter : PageSizes.A4;
        if (o.Orientation == "landscape") page = page.Landscape();
        float mm = 72f / 25.4f;
        float pageWidth = page.Width / mm, pageHeight = page.Height / mm;
        float contentWidth = pageWidth - (float)o.Margin * 2;
        float contentHeight = pageHeight - (float)o.Margin * 2;
        if (!string.IsNullOrEmpty(o.Title)) contentHeight -= 10;
        if (!string.IsNullOrEmpty(o.Footer) || o.PageNumbers) contentHeight -= 8;
        float cardWidth = (contentWidth - (o.Columns - 1) * (float)o.CardGap) / o.Columns;
        float cardHeight = (contentHeight - (o.Rows - 1) * (float)o.CardGap) / o.Rows;
        if (cardWidth < 70 || cardHeight < 57) throw new ArgumentException("Cards are too small for a legible QR code; reduce the grid or margins.");
        int perPage = checked(o.Columns * o.Rows);
        int pages = (coupons.Count + perPage - 1) / perPage;
        string accent = o.BlackAndWhite ? "#000000" : o.AccentColor;
        string background = o.BlackAndWhite ? "#FFFFFF" : o.BackgroundColor;
        byte[]? logo = o.Logo is null ? null : File.ReadAllBytes(o.Logo);
        var document = Document.Create(root =>
        {
            for (int p = 0; p < pages; p++)
            {
                int pageIndex = p;
                root.Page(pg =>
                {
                    pg.Size(page);
                    pg.Margin((float)o.Margin, Unit.Millimetre);
                    pg.DefaultTextStyle(x => x.FontFamily("Lato", "Noto Sans CJK JP").FontSize(8).FontColor("#111111"));
                    if (!string.IsNullOrEmpty(o.Title))
                        pg.Header().Height(10, Unit.Millimetre).Text(o.Title).Bold().FontSize(14).FontColor(accent);
                    pg.Content().Column(col =>
                    {
                        for (int r = 0; r < o.Rows; r++)
                        {
                            int row = r;
                            col.Item().Height(cardHeight, Unit.Millimetre).Row(grid =>
                            {
                                for (int c = 0; c < o.Columns; c++)
                                {
                                    if (c > 0) grid.ConstantItem((float)o.CardGap, Unit.Millimetre);
                                    int index = pageIndex * perPage + row * o.Columns + c;
                                    var cell = grid.RelativeItem();
                                    if (index < coupons.Count)
                                        Card(cell, coupons[index], o, cardWidth, cardHeight, accent, background, logo);
                                }
                            });
                            if (r < o.Rows - 1) col.Item().Height((float)o.CardGap, Unit.Millimetre);
                        }
                    });
                    if (!string.IsNullOrEmpty(o.Footer) || o.PageNumbers)
                        pg.Footer().Height(8, Unit.Millimetre).Row(row =>
                        {
                            row.RelativeItem().Text(o.Footer ?? "").FontSize(7);
                            if (o.PageNumbers) row.ConstantItem(25, Unit.Millimetre).AlignRight().Text($"Page {pageIndex + 1} / {pages}").FontSize(7);
                        });
                });
            }
        });
        document.GeneratePdf(o.Output!);
        return pages;
    }

    static void Card(IContainer cell, CouponRecord r, Options o, float w, float h, string accent, string background, byte[]? logo)
    {
        // One fixed box per record guarantees that no card crosses a page boundary.
        cell.Background(background).Border(0.5f).BorderColor(o.CutMarks ? "#000000" : "#AAAAAA")
            .Padding(3, Unit.Millimetre).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (logo is not null)
                        left.Item().Height(8, Unit.Millimetre).Image(logo).FitArea();
                    left.Item().Text(Short(r.Product, 70)).FontSize(10).Bold().FontColor(accent);
                    left.Item().PaddingTop(2, Unit.Millimetre).Text(r.Code)
                        .FontSize(r.Code.Length > 35 ? 8 : r.Code.Length > 20 ? 10 : 13).Bold().FontColor(accent);
                    if (r.Start is { } start) left.Item().Text($"Starts: {start:yyyy-MM-dd}").FontSize(7);
                    if (r.Expiry is { } expiry) left.Item().Text($"Expires: {expiry:yyyy-MM-dd}").FontSize(7);
                    if (o.IncludeGivenTo && r.GivenTo.Length > 0) left.Item().Text($"For: {Short(r.GivenTo, 40)}").FontSize(7);
                    if (o.IncludeIds)
                    {
                        if (r.CodeId.Length > 0) left.Item().Text($"Code ID: {Short(r.CodeId, 30)}").FontSize(6);
                        if (r.OrderId.Length > 0) left.Item().Text($"Order ID: {Short(r.OrderId, 30)}").FontSize(6);
                    }
                    var today = DateOnly.FromDateTime(DateTime.Today);
                    string status = r.Redeemed ? "REDEEMED" : !r.Available ? "UNAVAILABLE" : r.Expiry < today ? "EXPIRED" : r.Start > today ? "NOT YET ACTIVE" : "";
                    if (status.Length > 0) left.Item().Text(status).FontSize(7).Bold();
                });
                row.ConstantItem(32, Unit.Millimetre).Column(right =>
                {
                    right.Item().Width(30, Unit.Millimetre).Height(30, Unit.Millimetre).Image(QrPng(r.Url)).FitArea();
                    right.Item().Text("Scan to redeem").FontSize(6).AlignCenter();
                    if (o.IncludeUrl != "none")
                        right.Item().Text(o.IncludeUrl == "full" ? r.Url : Short(new Uri(r.Url).Host + new Uri(r.Url).AbsolutePath, 30)).FontSize(5);
                });
            });
    }
    static string Short(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
