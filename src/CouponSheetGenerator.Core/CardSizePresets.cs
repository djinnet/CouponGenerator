namespace CouponSheetGenerator;

public enum CardSizePreset
{
    Automatic,
    Classic,
    EuropeanBusiness,
    AmericanBusiness,
    StandardBusiness,
    Mini,
    Postcard,
    Folded,
    Square,
    Slim
}

public static class CardSizePresets
{
    public static (double WidthMm, double HeightMm) Size(CardSizePreset preset) => preset switch
    {
        // 89.66 mm rounds to the familiar 89.7 mm label and fits three rows on A4
        // with 10 mm margins and 4 mm gaps; 89.7 exactly would exceed the page by 0.1 mm.
        CardSizePreset.Classic => (93, 89.66),
        CardSizePreset.EuropeanBusiness => (85, 55),
        CardSizePreset.AmericanBusiness => (88.9, 50.8),
        CardSizePreset.StandardBusiness => (90, 50),
        CardSizePreset.Mini => (70, 40),
        CardSizePreset.Postcard => (100, 70),
        CardSizePreset.Folded => (85, 110),
        CardSizePreset.Square => (55, 55),
        CardSizePreset.Slim => (85, 35),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), "Choose a card size preset.")
    };
}
