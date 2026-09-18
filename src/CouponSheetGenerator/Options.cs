namespace CouponSheetGenerator;

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
}
