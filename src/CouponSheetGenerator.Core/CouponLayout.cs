using QuestPDF.Helpers;

namespace CouponSheetGenerator;

public sealed record LayoutMetrics(double CardWidthMm, double CardHeightMm, int CouponsPerPage, int PageCount, int FrontPageCount);

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

        var cardWidth = (width - (options.Columns - 1) * options.CardGap) / options.Columns;
        var cardHeight = (height - (options.Rows - 1) * options.CardGap) / options.Rows;
        if (cardWidth < 70 || cardHeight < 57)
        {
            throw new ArgumentException("Cards are too small for a legible QR code; reduce the grid or margins.");
        }

        if (options.CardPaddingMm is < 0 or > 20 || options.QrSizeMm < 20 || options.QrSizeMm + 2 * options.CardPaddingMm + 2 >= cardWidth || options.QrSizeMm + 2 * options.CardPaddingMm >= cardHeight)
        {
            throw new ArgumentException("QR size or card padding does not fit; reduce QR size or padding, or enlarge the cards.");
        }

        if (options.CodeFontSize is < 0 or > 30)
        {
            throw new ArgumentException("Code font size must be 0 (automatic) or at most 30 points.");
        }

        int perPage = checked(options.Columns * options.Rows);
        return new(cardWidth, cardHeight, perPage, (couponCount + perPage - 1) / perPage);
    }
}
