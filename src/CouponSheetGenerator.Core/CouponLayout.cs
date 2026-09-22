using QuestPDF.Helpers;

namespace CouponSheetGenerator;

public sealed record LayoutMetrics(
    double CardWidthMm, double CardHeightMm, int CouponsPerPage, int PageCount, int FrontPageCount,
    int Columns, int Rows, double QrSizeMm, bool Compact);

public static class CouponLayout
{
    public static LayoutMetrics Calculate(Options options, int couponCount)
    {
        if (options.PageSize is not ("A4" or "Letter") || options.Orientation is not ("portrait" or "landscape"))
        {
            throw new ArgumentException("Choose A4 or Letter and portrait or landscape.");
        }

        if (options.Columns is < 1 or > 10 || options.Rows is < 1 or > 10 || options.Margin < 0 || options.CardGap < 0)
        {
            throw new ArgumentException("Choose 1–10 rows and columns, with nonnegative margin and gap.");
        }

        var page = options.PageSize == "Letter" ? PageSizes.Letter : PageSizes.A4;
        if (options.Orientation == "landscape")
        {
            page = page.Landscape();
        }

        const double pointsPerMillimetre = 72.0 / 25.4;
        var width = page.Width / pointsPerMillimetre - 2 * options.Margin;
        var height = page.Height / pointsPerMillimetre - 2 * options.Margin;
        if (!string.IsNullOrEmpty(options.Title))
        {
            height -= 10;
        }

        if (!string.IsNullOrEmpty(options.Footer) || options.PageNumbers)
        {
            height -= 8;
        }

        bool automatic = options.CardSizePreset == CardSizePreset.Automatic;
        double cardWidth, cardHeight;
        int columns, rows;
        if (automatic)
        {
            columns = options.Columns;
            rows = options.Rows;
            cardWidth = (width - (columns - 1) * options.CardGap) / columns;
            cardHeight = (height - (rows - 1) * options.CardGap) / rows;
        }
        else
        {
            (cardWidth, cardHeight) = CardSizePresets.Size(options.CardSizePreset);
            columns = Math.Min(10, (int)Math.Floor((width + options.CardGap) / (cardWidth + options.CardGap)));
            rows = Math.Min(10, (int)Math.Floor((height + options.CardGap) / (cardHeight + options.CardGap)));
            if (columns < 1 || rows < 1)
                throw new ArgumentException("The selected card size does not fit this page, margin, title, and footer combination.");
        }

        if (automatic && (cardWidth < 70 || cardHeight < 57))
        {
            throw new ArgumentException("Cards are too small for a legible QR code; reduce the grid or margins.");
        }

        bool compact = !automatic && (cardWidth <= 70 || cardHeight <= 40);
        double qrSize = compact ? Math.Min(options.QrSizeMm, Math.Min(26, cardHeight - 2 * options.CardPaddingMm - 2)) : options.QrSizeMm;
        if (options.CardPaddingMm is < 0 or > 20 || options.QrSizeMm < 20 || qrSize < 20 ||
            qrSize + 2 * options.CardPaddingMm + 2 >= cardWidth || qrSize + 2 * options.CardPaddingMm >= cardHeight)
        {
            throw new ArgumentException("QR size or card padding does not fit; reduce QR size or padding, or enlarge the cards.");
        }

        if (options.CodeFontSize is < 0 or > 30)
        {
            throw new ArgumentException("Code font size must be 0 (automatic) or at most 30 points.");
        }

        if (options.LogoWidthMm is < 0 or > 100 || options.LogoHeightMm is < 1 or > 40 || options.LogoWidthMm > 0 && !compact && options.LogoWidthMm + qrSize + 2 * options.CardPaddingMm + 2 >= cardWidth)
        {
            throw new ArgumentException("Logo size does not fit; reduce logo size or enlarge the cards.");
        }

        if (options.BorderWidthPt is < 0 or > 6 || options.CornerRadiusMm is < 0 or > 15)
        {
            throw new ArgumentException("Border width must be 0–6 points and corner radius must be 0–15 mm.");
        }

        if (options.BackEnabled && ((options.BackTitle?.Length ?? 0) > 200 || (options.BackText?.Length ?? 0) > 1000))
        {
            throw new ArgumentException("Back title must be at most 200 characters and back text must be at most 1000 characters.");
        }

        foreach (var color in new[] { options.BackgroundColor, options.AccentColor, options.BorderColor, options.BackBackgroundColor, options.BackAccentColor, options.BackBorderColor })
        {
            if (color is null || !color.StartsWith('#') || (color.Length != 7 && color.Length != 9) || !int.TryParse(color[1..], System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                throw new ArgumentException($"Invalid color: {color}. Use #RRGGBB or #RRGGBBAA format.");
            }
        }

        int perPage = checked(columns * rows);
        int frontPageCount = (couponCount + perPage - 1) / perPage;
        return new(cardWidth, cardHeight, perPage, frontPageCount * (options.BackEnabled ? 2 : 1), frontPageCount, columns, rows, qrSize, compact);
    }
}
