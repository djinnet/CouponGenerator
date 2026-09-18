# Coupon Sheet Generator

A local .NET 10 console application that converts TSV promotional-code records into printable PDF coupon sheets. It does not send TSV data, codes, or URLs to any network service. The sample uses fictional codes and the reserved `example.invalid` domain.

## Prerequisites and build

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). From the repository root:

```powershell
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet test --no-restore
```

## Run

```powershell
dotnet run --project src/CouponSheetGenerator -- validate --input sample/fictional-codes.tsv
dotnet run --project src/CouponSheetGenerator -- generate --input sample/fictional-codes.tsv --output sample/example-coupons.pdf --title "Fictional coupons" --page-numbers
```

Run with no arguments to see all options. `--config sample/config.example.json` loads reusable JSON settings; command-line arguments override corresponding settings. The JSON keys use the property names shown in the example. `--input` and `--output` can also be supplied in JSON.

## TSV schema

Use UTF-8 TSV with a header row. Required headers: `Product name`, `Promotional code`, `Redeemable URL`. Optional headers: `Order name`, `Start date`, `Expire date`, `Code ID`, `Order ID`, `Given to`, `Available`, and `Redeemed`. Headers are matched case-insensitively after trimming, and a UTF-8 BOM is accepted. Extra columns are ignored. Quoted tabs, quotes, and newlines are supported. The code is preserved exactly; no trimming or numeric conversion is applied. Product and URL fields are trimmed.

Booleans accept true/false, yes/no, and 1/0 without regard to case. A blank `Available` defaults to true, and a blank `Redeemed` defaults to false. Invalid optional booleans produce a row warning and use those defaults. Dates accept `yyyy-MM-dd`, `yyyy/MM/dd`, and English month-name forms such as `18 Sep 2026` or `Sep 18 2026`. Numeric day/month forms such as `02/03/2026` are rejected as ambiguous; invalid optional dates produce a row warning and are ignored.

Rows without a product, code, or absolute HTTPS redeemable URL are skipped with a row warning. Other valid rows continue processing. `validate` checks the input without producing a PDF and exits nonzero if it finds invalid rows. Fatal errors, including missing headers, missing input, no eligible coupons, and output failure, exit nonzero. Normal diagnostics omit codes and URLs.

## Eligibility and layout

By default, redeemed, unavailable, expired, and future-starting records are excluded. Use `--include-redeemed`, `--include-unavailable`, `--include-expired`, or `--include-future` to include them. `--exclude-redeemed` and `--exclude-unavailable` explicitly restore the defaults when a configuration file enables inclusion.

The default page is A4 portrait with two columns, three rows, 10 mm margins, and 4 mm gaps. Each card is about 93 × 89.7 mm, with six per page. With a title or footer, height is slightly reduced. The grid uses fixed card boxes so cards never split across pages; the last page can be partially filled. The minimum accepted card size is 70 × 57 mm. Product names and optional display fields are shortened with an ellipsis after a fixed character limit; actual promotional-code text is never shortened. For best results, keep promotional codes reasonably short so they fit at a readable size.

`--page-size` accepts A4 or Letter; `--orientation` accepts portrait or landscape. `--margin` and `--card-gap` use millimetres. `--columns` and `--rows` set the cutting grid. `--cut-marks` darkens card borders as cutting guides. `--page-numbers`, `--title`, `--footer`, `--logo`, `--background-color`, `--accent-color`, and `--black-and-white` customize appearance. `--include-given-to` and `--include-ids` reveal optional data. `--include-url short|full` prints a URL under the QR code; the default `none` keeps it hidden. The QR code always encodes the complete HTTPS URL exactly as read after surrounding whitespace is trimmed.

Print the PDF at **100% / actual size** and disable “fit to page” so the card dimensions stay accurate. The QR images have a quiet zone and error correction level Q.

## Privacy and security

Processing is local. No URL is opened, no PDF is opened automatically, and no temporary QR image files are written. TSV cell values are treated as data, never paths or commands. Only HTTPS redemption URLs are accepted. Keep your actual TSV files and output PDFs private because both contain redeemable credentials.

## Libraries and licenses

- [CsvHelper](https://joshclose.github.io/CsvHelper/): robust quoted TSV parsing; Apache 2.0 or MS-PL, including commercial use.
- [QRCoder](https://github.com/Shane32/QRCoder): in-memory QR generation; MIT.
- [QuestPDF](https://www.questpdf.com/license/community.html): precise PDF grid, pagination, text, and embedded fonts; **Community License has eligibility limits**. It is free for eligible individuals and organizations under USD 1 million annual gross revenue, qualifying charities, academic institutions, and qualifying open-source projects. Public-sector bodies and publicly traded companies generally need a paid license. Review the current license before production use.
- [Noto Sans CJK JP](https://github.com/notofonts/noto-cjk): bundled fallback font for CJK text, under the SIL Open Font License 1.1. Its [license notice](src/CouponSheetGenerator/Fonts/OFL.txt) is included.
- Tests use xUnit, ZXing.Net (Apache 2.0), and SkiaSharp (MIT) to decode generated QR images.

## Troubleshooting

- “Missing required headers”: check header spelling and tabs, and ensure the first row is the header.
- “No eligible coupons”: inspect the validation summary and date/status fields; use inclusion flags only when intended.
- “Cards are too small”: reduce rows/columns or margins.
- PDF looks clipped when printed: select actual size / 100% and check printer margins.
- A build cannot reach NuGet: restore packages while connected, then use `--no-restore` for subsequent build/test commands.
