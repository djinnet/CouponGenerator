using System.Text.Json.Serialization;

namespace CouponSheetGenerator;

public enum PageSizeKind { A4, Letter }
public enum PageOrientation { Portrait, Landscape }
public enum CardImageFit { Contain, Stretch }

public sealed class Options
{
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string PageSize { get; set; } = "A4";
    public string Orientation { get; set; } = "portrait";
    public int Columns { get; set; } = 2;
    public int Rows { get; set; } = 3;
    public double Margin { get; set; } = 10;
    public double CardGap { get; set; } = 4;
    public bool CutMarks { get; set; }
    public bool PageNumbers { get; set; }
    public string? Logo { get; set; }
    public string? Title { get; set; }
    public string? Footer { get; set; }
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string AccentColor { get; set; } = "#183153";
    public bool IncludeGivenTo { get; set; }
    public string IncludeUrl { get; set; } = "none";
    public bool IncludeIds { get; set; }
    public bool IncludeRedeemed { get; set; }
    public bool IncludeUnavailable { get; set; }
    public bool IncludeExpired { get; set; }
    public bool IncludeFuture { get; set; }
    public bool BlackAndWhite { get; set; }
    public bool Verbose { get; set; }
    public bool ShowProductName { get; set; } = true;
    public bool ShowDates { get; set; } = true;
    public bool ShowStatus { get; set; } = true;
    public bool ShowOrderName { get; set; }
    public string InstructionText { get; set; } = "Scan to redeem";
    public double QrSizeMm { get; set; } = 30;
    public double CardPaddingMm { get; set; } = 3;
    public double CodeFontSize { get; set; }
    [JsonIgnore]
    public PageSizeKind PageSizeValue
    {
        get => PageSize == "Letter" ? PageSizeKind.Letter : PageSizeKind.A4;
        set => PageSize = value.ToString();
    }
    [JsonIgnore]
    public PageOrientation OrientationValue
    {
        get => Orientation == "landscape" ? PageOrientation.Landscape : PageOrientation.Portrait;
        set => Orientation = value.ToString().ToLowerInvariant();
    }
    public double LogoWidthMm { get; set; }
    public double LogoHeightMm { get; set; } = 8;
    public string BorderColor { get; set; } = "#AAAAAA";
    public double BorderWidthPt { get; set; } = 0.5;
    public double CornerRadiusMm { get; set; }
    public string? CardBackgroundImage { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<CardImageFit>))]
    public CardImageFit CardBackgroundImageFit { get; set; } = CardImageFit.Contain;
    public bool BackEnabled { get; set; }
    public string BackTitle { get; set; } = "Thank you";
    public string BackText { get; set; } = "Present this coupon at checkout.";
    public string BackBackgroundColor { get; set; } = "#FFFFFF";
    public string BackAccentColor { get; set; } = "#183153";
    public string BackBorderColor { get; set; } = "#AAAAAA";
    public string? BackBackgroundImage { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<CardImageFit>))]
    public CardImageFit BackBackgroundImageFit { get; set; } = CardImageFit.Contain;
    public bool BackShowLogo { get; set; }
    public bool BackMirrorColumns { get; set; } = true;
}