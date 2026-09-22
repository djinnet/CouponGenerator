using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CouponSheetGenerator;

public static class PdfWriter
{
    public static byte[] QrPng(string url, double printSizeMm = 30)
    {
        using var qr = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        if (printSizeMm / qr.ModuleMatrix.Count < 0.35)
        {
            throw new ArgumentException("QR code is too dense for its print area; shorten the redeemable URL or use a larger QR size.");
        }

        return new PngByteQRCode(qr).GetGraphic(12, drawQuietZones: true);
    }

    public static int Write(IReadOnlyList<CouponRecord> coupons, Options o)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var page = o.PageSize == "Letter" ? PageSizes.Letter : PageSizes.A4;
        if (o.Orientation == "landscape")
        {
            page = page.Landscape();
        }

        var metrics = CouponLayout.Calculate(o, coupons.Count);
        float cardHeight = (float)metrics.CardHeightMm;
        int perPage = metrics.CouponsPerPage;
        int pages = metrics.PageCount;
        string accent = o.BlackAndWhite ? "#000000" : o.AccentColor;
        string background = o.BlackAndWhite ? "#FFFFFF" : o.BackgroundColor;
        byte[]? logo = string.IsNullOrWhiteSpace(o.Logo) ? null : LocalImage.ReadValidated(o.Logo);
        byte[]? frontImage = o.BlackAndWhite || string.IsNullOrWhiteSpace(o.CardBackgroundImage) ? null : LocalImage.ReadValidated(o.CardBackgroundImage);
        byte[]? backImage = o.BlackAndWhite || string.IsNullOrWhiteSpace(o.BackBackgroundImage) ? null : LocalImage.ReadValidated(o.BackBackgroundImage);
        var document = Document.Create(root =>
        {
            for (int p = 0; p < metrics.FrontPageCount; p++)
            {
                int pageIndex = p;
                for (int face = 0; face < (o.BackEnabled ? 2 : 1); face++)
                {
                  bool isBack = face == 1;
                  root.Page(pg =>
                  {
                    pg.Size(page);
                    pg.Margin((float)o.Margin, Unit.Millimetre);
                    pg.DefaultTextStyle(x => x.FontFamily("Lato", "Noto Sans CJK JP").FontSize(8).FontColor("#111111"));
                    if (!string.IsNullOrEmpty(o.Title))
                    {
                        pg.Header().Height(10, Unit.Millimetre).Text(isBack ? "" : o.Title).Bold().FontSize(14).FontColor(accent);
                    }

                    pg.Content().Column(col =>
                    {
                        for (int r = 0; r < o.Rows; r++)
                        {
                            int row = r;
                            col.Item().Height(cardHeight, Unit.Millimetre).Row(grid =>
                            {
                                for (int c = 0; c < o.Columns; c++)
                                {
                                    if (c > 0)
                                    {
                                        grid.ConstantItem((float)o.CardGap, Unit.Millimetre);
                                    }

                                    int sourceColumn = isBack && o.BackMirrorColumns ? o.Columns - 1 - c : c;
                                    int index = pageIndex * perPage + row * o.Columns + sourceColumn;
                                    var cell = grid.RelativeItem();
                                    if (index < coupons.Count)
                                    {
                                        if (isBack) BackCard(cell, o, logo, backImage);
                                        else Card(cell, coupons[index], o, accent, background, logo, frontImage);
                                    }
                                }
                            });
                            if (r < o.Rows - 1) col.Item().Height((float)o.CardGap, Unit.Millimetre);
                        }
                    });
                    if (!string.IsNullOrEmpty(o.Footer) || o.PageNumbers)
                    {
                        pg.Footer().Height(8, Unit.Millimetre).Row(row =>
                        {
                            row.RelativeItem().Text(isBack ? "" : o.Footer ?? "").FontSize(7);
                            if (o.PageNumbers)
                            {
                                row.ConstantItem(25, Unit.Millimetre).AlignRight().Text(isBack ? "" : $"Page {pageIndex + 1} / {metrics.FrontPageCount}").FontSize(7);
                            }
                        });
                    }
                  });
                }
            }
        });
        document.GeneratePdf(o.Output!);
        return pages;
    }

    static void Card(IContainer cell, CouponRecord r, Options o, string accent, string background, byte[]? logo, byte[]? frontImage)
    {
        // One fixed box per record guarantees that no card crosses a page boundary.
        var styled = cell.CornerRadius((float)o.CornerRadiusMm, Unit.Millimetre)
            .Border((float)o.BorderWidthPt).BorderColor(o.CutMarks ? "#000000" : o.BorderColor).Background(background);
        if (frontImage is null)
            FrontContent(styled.Padding((float)o.CardPaddingMm, Unit.Millimetre), r, o, accent, logo, false);
        else
            styled.Layers(layers =>
            {
                var image = layers.Layer().CornerRadius((float)o.CornerRadiusMm, Unit.Millimetre).Image(frontImage);
                if (o.CardBackgroundImageFit == CardImageFit.Stretch) image.FitUnproportionally(); else image.FitArea();
                FrontContent(layers.PrimaryLayer().Padding((float)o.CardPaddingMm, Unit.Millimetre), r, o, accent, logo, true);
            });
    }

    static void FrontContent(IContainer content, CouponRecord r, Options o, string accent, byte[]? logo, bool hasImage)
    {
        content.Row(row =>
        {
            var leftPanel = row.RelativeItem();
            if (hasImage) leftPanel = leftPanel.Background("#E6FFFFFF");
            leftPanel.Column(left =>
            {
                if (logo is not null)
                {
                    var logoSlot = left.Item().Height((float)o.LogoHeightMm, Unit.Millimetre);
                    if (o.LogoWidthMm > 0) logoSlot = logoSlot.Width((float)o.LogoWidthMm, Unit.Millimetre);
                    logoSlot.Image(logo).FitArea();
                }

                if (o.ShowProductName) left.Item().Text(Short(r.Product, 70)).FontSize(10).Bold().FontColor(accent);
                if (o.ShowOrderName && r.OrderName.Length > 0) left.Item().Text(Short(r.OrderName, 50)).FontSize(7);
                left.Item().PaddingTop(2, Unit.Millimetre).Text(r.Code)
                    .FontSize(o.CodeFontSize > 0 ? (float)o.CodeFontSize : r.Code.Length > 35 ? 8 : r.Code.Length > 20 ? 10 : 13).Bold().FontColor(accent);
                if (o.ShowDates && r.Start is { } start)
                {
                    left.Item().Text($"Starts: {start:yyyy-MM-dd}").FontSize(7);
                }

                if (o.ShowDates && r.Expiry is { } expiry)
                {
                    left.Item().Text($"Expires: {expiry:yyyy-MM-dd}").FontSize(7);
                }

                if (o.IncludeGivenTo && r.GivenTo.Length > 0)
                {
                    left.Item().Text($"For: {Short(r.GivenTo, 40)}").FontSize(7);
                }

                if (o.IncludeIds)
                {
                    if (r.CodeId.Length > 0)
                    {
                        left.Item().Text($"Code ID: {Short(r.CodeId, 30)}").FontSize(6);
                    }

                    if (r.OrderId.Length > 0)
                    {
                        left.Item().Text($"Order ID: {Short(r.OrderId, 30)}").FontSize(6);
                    }
                }
                var today = DateOnly.FromDateTime(DateTime.Today);
                string status = r.Redeemed ? "REDEEMED" : !r.Available ? "UNAVAILABLE" : r.Expiry < today ? "EXPIRED" : r.Start > today ? "NOT YET ACTIVE" : "";
                if (o.ShowStatus && status.Length > 0)
                {
                    left.Item().Text(status).FontSize(7).Bold();
                }
            });
            row.ConstantItem((float)o.QrSizeMm + 2, Unit.Millimetre).Column(right =>
            {
                right.Item().Width((float)o.QrSizeMm, Unit.Millimetre).Height((float)o.QrSizeMm, Unit.Millimetre).Image(QrPng(r.Url, o.QrSizeMm)).FitArea();
                if (o.InstructionText.Length > 0) right.Item().Text(o.InstructionText).FontSize(6).AlignCenter();
                if (o.IncludeUrl != "none")
                {
                    right.Item().Text(o.IncludeUrl == "full" ? r.Url : Short(new Uri(r.Url).Host + new Uri(r.Url).AbsolutePath, 30)).FontSize(5);
                }
            });
        });
    }
    static void BackCard(IContainer cell, Options o, byte[]? logo, byte[]? backImage)
    {
        string background = o.BlackAndWhite ? "#FFFFFF" : o.BackBackgroundColor;
        string accent = o.BlackAndWhite ? "#000000" : o.BackAccentColor;
        var styled = cell.CornerRadius((float)o.CornerRadiusMm, Unit.Millimetre)
            .Border((float)o.BorderWidthPt).BorderColor(o.CutMarks ? "#000000" : o.BackBorderColor).Background(background);
        if (backImage is null)
            BackContent(styled.Padding((float)o.CardPaddingMm, Unit.Millimetre), o, logo, accent, false);
        else
            styled.Layers(layers =>
            {
                var image = layers.Layer().CornerRadius((float)o.CornerRadiusMm, Unit.Millimetre).Image(backImage);
                if (o.BackBackgroundImageFit == CardImageFit.Stretch) image.FitUnproportionally(); else image.FitArea();
                BackContent(layers.PrimaryLayer().Padding((float)o.CardPaddingMm, Unit.Millimetre), o, logo, accent, true);
            });
    }
    static void BackContent(IContainer content, Options o, byte[]? logo, string accent, bool hasImage)
    {
        content.AlignCenter().AlignMiddle().Column(column =>
        {
            if (o.BackShowLogo && logo is not null)
            {
                var logoSlot = column.Item().AlignCenter().Height((float)o.LogoHeightMm, Unit.Millimetre);
                if (o.LogoWidthMm > 0) logoSlot = logoSlot.Width((float)o.LogoWidthMm, Unit.Millimetre);
                logoSlot.Image(logo).FitArea();
            }
            if (!string.IsNullOrEmpty(o.BackTitle))
            {
                var title = column.Item().AlignCenter();
                if (hasImage) title = title.Background("#E6FFFFFF");
                title.Text(o.BackTitle).FontSize(14).Bold().FontColor(accent).AlignCenter();
            }
            if (!string.IsNullOrEmpty(o.BackText))
            {
                var text = column.Item().PaddingTop(3, Unit.Millimetre).PaddingHorizontal(5, Unit.Millimetre).AlignCenter();
                if (hasImage) text = text.Background("#E6FFFFFF");
                text.Text(o.BackText).FontSize(8).AlignCenter();
            }
        });
    }

    static string Short(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
