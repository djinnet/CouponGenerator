# Coupon Sheet Generator

A local .NET 10 solution that converts TSV promotional-code records into printable PDF coupon sheets. The original CLI remains at `src/CouponSheetGenerator`; the Avalonia desktop app is at `src/CouponSheetGenerator.Desktop`. Shared TSV, filtering, QR, and PDF code lives in `src/CouponSheetGenerator.Core`. Neither application sends TSV data, codes, or URLs to a network service. The sample uses fictional codes and the reserved `example.invalid` domain.

## Prerequisites and build

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). From the repository root:

```powershell
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet test --no-restore
dotnet run --project src/CouponSheetGenerator.Desktop
```

## Run

```powershell
dotnet run --project src/CouponSheetGenerator -- validate --input sample/fictional-codes.tsv
dotnet run --project src/CouponSheetGenerator -- generate --input sample/fictional-codes.tsv --output sample/example-coupons.pdf --title "Fictional coupons" --page-numbers
```

Run with no arguments to see all options. `--config sample/config.example.json` loads reusable JSON settings; command-line arguments override corresponding settings. The JSON keys use the property names shown in the example. `--input` and `--output` can also be supplied in JSON.

The desktop app can import TSV by picker or drag-and-drop, review eligible records and warnings, edit the shared page and content settings, render the generated PDF in its preview tab, and export with a Save As dialog. Import and Save As in Settings use the same JSON format as the CLI. GUI edits remain in memory until Save As. The record-table filter affects display only; export always includes every eligible record in source order.

Page size and orientation use enum-backed dropdowns in the desktop app. The shared JSON and CLI still use the existing `"A4"`/`"Letter"` and `"portrait"`/`"landscape"` strings. Card size is a separate dropdown: Automatic retains the existing rows-and-columns layout; fixed presets use exact card dimensions and choose the largest grid that fits the page, margins, gap, title, and footer. The preset is stored as `CardSizePreset` in shared JSON; omitting it keeps Automatic. CLI flags and existing configuration files keep their behavior.

Fixed card sizes include Classic (93 × approximately 89.7 mm), European business (85 × 55 mm), American business (88.9 × 50.8 mm), Standard business (90 × 50 mm), Mini (70 × 40 mm), Postcard (100 × 70 mm), Folded (85 × 110 mm), Square (55 × 55 mm), and Slim (85 × 35 mm). Classic uses 89.66 mm height so the familiar rounded 89.7 mm card fits three rows on A4 with the default margins and gaps. The fixed presets derive their row and column counts from available page space, so the Rows and Columns controls apply only to Automatic. Mini, Square, and Slim use a compact face showing the product, full promotional code, QR, and optional logo; optional dates, status, IDs, instruction, and URL text are omitted from those small faces. Their effective QR size may be reduced to fit and is shown in the layout summary. Keep redeemable URLs short enough for the smaller QR codes to scan reliably.

Coupon design includes Minimal, Modern, and Ink-saving presets, plus product/date/status visibility, order-name display, instruction text, code font size, QR size, and card padding. These extra settings are saved in the shared JSON and default to the CLI's historical appearance.

Front card settings now include logo width and height, border color and width, corner radius, and a local PNG/JPEG background image. The Card back tab can add a matching back page after each front page, with its own title, message, colors, border color, image, and optional logo. Back columns are mirrored by default for long-edge duplex printing; the checkbox can be turned off for a different printer flip setup. A back-enabled PDF alternates front and back pages, so its physical page count doubles. Print one test sheet at **Actual size / 100%** and confirm alignment with the printer's duplex setting before printing the batch. Background image fit offers `Contain` (keeps proportions) and `Stretch` (fills the card, possibly distorting the image). Text is placed on a translucent white panel for contrast, and QR codes retain their opaque white quiet zone. Black-and-white mode suppresses background images.

An example is generated with:

```powershell
dotnet run --project src/CouponSheetGenerator -- generate --config sample/config.two-sided.json --input sample/fictional-codes.tsv --output sample/two-sided-example.pdf
```

For a platform-specific release, use `dotnet publish src/CouponSheetGenerator.Desktop -c Release -r win-x64 --self-contained true` (or `linux-x64` / `osx-x64`). Windows is the only platform built and tested in this workspace. PDFium native libraries are bundled through PDFtoImage for Windows, Linux, and macOS; verify a published package on its target platform before distribution.

## GitHub releases

The **Release Windows EXE** workflow runs manually from **Actions → Release Windows EXE → Run workflow**. Commit and push the workflow to the repository's default branch first so GitHub shows the Run workflow button, then select the branch to release.

Set the application version in [`Directory.Build.props`](Directory.Build.props) before each release (initially `1.0.0`). The workflow reads the desktop project's evaluated `Version`, tests the solution, and publishes a single self-contained Windows x64 desktop executable. It creates a tag such as `v1.0.0` at the selected run's commit, generates release notes, and attaches `CouponSheetGenerator-v1.0.0-win-x64.exe`, SHA-256 checksums, and license notices. Versions with a suffix such as `1.1.0-beta.1` create prereleases. Existing tags are rejected; increase the version for the next release.

The EXE includes .NET, native libraries, and bundled font content; users do not need to install .NET. Bundled files extract automatically on launch. The workflow uploads assets to a draft before publishing it. If an upload or publishing step fails, inspect the draft in GitHub Releases before retrying. It uses the built-in GitHub token with `contents: write`; no personal access token is needed. Repository rules must allow that token to create release tags.

## TSV schema

Use UTF-8 TSV with a header row. Required headers: `Product name`, `Promotional code`, `Redeemable URL`. Optional headers: `Order name`, `Start date`, `Expire date`, `Code ID`, `Order ID`, `Given to`, `Available`, and `Redeemed`. Headers are matched case-insensitively after trimming, and a UTF-8 BOM is accepted. Extra columns are ignored. Quoted tabs, quotes, and newlines are supported. The code is preserved exactly; no trimming or numeric conversion is applied. Product and URL fields are trimmed.

Booleans accept true/false, yes/no, and 1/0 without regard to case. A blank `Available` defaults to true, and a blank `Redeemed` defaults to false. Invalid optional booleans produce a row warning and use those defaults. Dates accept `yyyy-MM-dd`, `yyyy/MM/dd`, and English month-name forms such as `18 Sep 2026` or `Sep 18 2026`. Exported US timestamps such as `9/19/2026 8:00 AM` are also accepted, interpreted as local time, displayed with their time, and used for exact start/expiry filtering. `1/1/0001 12:00 AM` is treated as an unset date. A date-only expiry remains valid through the end of that date. Bare numeric slash dates such as `02/03/2026` remain rejected as ambiguous; invalid optional dates produce a row warning and are ignored.

Absent optional columns are reported. Duplicate promotional codes and Code IDs are reported and retained as separate records, matching the CLI's previous behavior. Input is limited to 32 MiB, 100,000 records, and 16,384 characters per field. These limits are currently fixed. Invalid rows are excluded while valid rows remain available for generation.

Rows without a product, code, or absolute HTTP or HTTPS redeemable URL are skipped with a row warning. Other valid rows continue processing. `validate` checks the input without producing a PDF and exits nonzero if it finds invalid rows. Fatal errors, including missing headers, missing input, no eligible coupons, and output failure, exit nonzero. Normal diagnostics omit codes and URLs.

## Eligibility and layout

By default, redeemed, unavailable, expired, and future-starting records are excluded. Use `--include-redeemed`, `--include-unavailable`, `--include-expired`, or `--include-future` to include them. `--exclude-redeemed` and `--exclude-unavailable` explicitly restore the defaults when a configuration file enables inclusion.

The default page is A4 portrait with two columns, three rows, 10 mm margins, and 4 mm gaps. Each card is about 93 × 89.7 mm, with six per page. With a title or footer, height is slightly reduced. The grid uses fixed card boxes so cards never split across pages; the last page can be partially filled. The minimum accepted card size is 70 × 57 mm. Product names and optional display fields are shortened with an ellipsis after a fixed character limit; actual promotional-code text is never shortened. For best results, keep promotional codes reasonably short so they fit at a readable size.

`--page-size` accepts A4 or Letter; `--orientation` accepts portrait or landscape. `--margin` and `--card-gap` use millimetres. `--columns` and `--rows` set the cutting grid. `--cut-marks` darkens card borders as cutting guides. `--page-numbers`, `--title`, `--footer`, `--logo`, `--background-color`, `--accent-color`, and `--black-and-white` customize appearance. `--include-given-to` and `--include-ids` reveal optional data. `--include-url short|full` prints a URL under the QR code; the default `none` keeps it hidden. The QR code always encodes the complete HTTP or HTTPS URL exactly as read after surrounding whitespace is trimmed.

Print the PDF at **100% / actual size** and disable “fit to page” so the card dimensions stay accurate. The QR images have a quiet zone and error correction level Q.

## Privacy and security

Processing is local. No URL is opened, no PDF is opened automatically, and no temporary QR image files are written. TSV cell values are treated as data, never paths or commands. HTTP and HTTPS redemption URLs are accepted. Keep your actual TSV files and output PDFs private because both contain redeemable credentials.

## Libraries and licenses

- [CsvHelper](https://joshclose.github.io/CsvHelper/): robust quoted TSV parsing; Apache 2.0 or MS-PL, including commercial use.
- [QRCoder](https://github.com/Shane32/QRCoder): in-memory QR generation; MIT.
- [QuestPDF](https://www.questpdf.com/license/community.html): precise PDF grid, pagination, text, and embedded fonts; **Community License has eligibility limits**. It is free for eligible individuals and organizations under USD 1 million annual gross revenue, qualifying charities, academic institutions, and qualifying open-source projects. Public-sector bodies and publicly traded companies generally need a paid license. Review the current license before production use.
- [Noto Sans CJK JP](https://github.com/notofonts/noto-cjk): bundled fallback font for CJK text, under the SIL Open Font License 1.1. Its [license notice](src/CouponSheetGenerator.Core/Fonts/OFL.txt) is included.
- [Avalonia](https://avaloniaui.net/): desktop UI; MIT. [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet): observable state and commands; MIT.
- Avalonia Accelerate Community displays a build-time telemetry notice during `dotnet build`. This concerns the build tooling; the coupon applications do not send coupon data or runtime analytics. See [Avalonia's Community Edition documentation](https://v11.docs.avaloniaui.net/accelerate/community/) for its build telemetry terms.
- [PDFtoImage](https://github.com/sungaila/PDFtoImage): embedded local PDF preview using PDFium and SkiaSharp; MIT. PDFium is BSD-style licensed and distributed as native assets. `Tmds.DBus.Protocol` is pinned to patched version 0.21.3.
- Tests use xUnit, ZXing.Net (Apache 2.0), and SkiaSharp (MIT) to decode generated QR images and QR codes on rendered PDF pages.

Run `dotnet list package --vulnerable --include-transitive` periodically and review pinned package versions before publishing.

## CLI to GUI settings mapping

The JSON key is the `Options` property name. Desktop controls are in the Coupon design or Page & print layout tabs. Defaults are unchanged from the CLI.

| CLI argument | JSON key | GUI control | Default | PDF effect |
| --- | --- | --- | --- | --- |
| `--page-size` | `PageSize` | Page layout: Page size dropdown | A4 | Paper size |
| `--orientation` | `Orientation` | Page layout: Orientation dropdown | portrait | Paper orientation |
| `--columns`, `--rows` | `Columns`, `Rows` | Page layout: Columns, Rows | 2, 3 | Cards per page |
| No CLI flag; JSON only | `CardSizePreset` | Page layout: Card size preset dropdown | Automatic | Exact fixed card size and fitted grid, or legacy automatic grid |
| `--margin`, `--card-gap` | `Margin`, `CardGap` | Page layout: Margin, Card gap | 10 mm, 4 mm | Page and card spacing |
| `--cut-marks`, `--page-numbers` | `CutMarks`, `PageNumbers` | Page layout: checkboxes | false | Borders and page labels |
| `--title`, `--footer` | `Title`, `Footer` | Coupon design: Title, Footer | empty | Header and footer |
| `--background-color`, `--accent-color` | `BackgroundColor`, `AccentColor` | Coupon design: color text fields | `#FFFFFF`, `#183153` | Card colors |
| `--black-and-white` | `BlackAndWhite` | Coupon design: checkbox | false | Monochrome mode |
| `--include-given-to`, `--include-ids` | `IncludeGivenTo`, `IncludeIds` | Coupon design: checkboxes | false | Optional card text |
| `--logo`, `--include-url` | `Logo`, `IncludeUrl` | Coupon design: Logo picker, URL text | empty, none | Logo and URL text |
| `--include-redeemed`, `--include-unavailable`, `--include-expired`, `--include-future` | corresponding `Include*` keys | Page layout: Eligibility filters | false | Eligibility filters |
| `--input`, `--output` | `Input`, `Output` | Import picker, Export Save As | empty | File paths |
| `--config` | n/a | Settings: Import JSON | none | Loads shared options |
| `--verbose` | `Verbose` | CLI only | false | No PDF effect |
| No CLI flag; JSON only | `ShowProductName`, `ShowDates`, `ShowStatus`, `ShowOrderName` | Coupon design: visibility checkboxes | true, true, true, false | Card content |
| No CLI flag; JSON only | `InstructionText`, `CodeFontSize` | Coupon design: text and size | Scan to redeem, automatic | Card text |
| No CLI flag; JSON only | `QrSizeMm`, `CardPaddingMm` | Coupon design: QR size, card padding | 30 mm, 3 mm | Card geometry |
| No CLI flag; JSON only | `LogoWidthMm`, `LogoHeightMm` | Coupon design: Logo width and height | automatic, 8 mm | Logo dimensions |
| No CLI flag; JSON only | `BorderColor`, `BorderWidthPt`, `CornerRadiusMm` | Coupon design: Card border settings | `#AAAAAA`, 0.5 pt, 0 mm | Front and back border shape and front color |
| No CLI flag; JSON only | `CardBackgroundImage`, `CardBackgroundImageFit` | Coupon design: Background image and fit | none, Contain | Front image |
| No CLI flag; JSON only | `BackEnabled`, `BackTitle`, `BackText`, `BackShowLogo`, `BackMirrorColumns` | Card back tab | false, Thank you, default message, false, true | Optional back pages |
| No CLI flag; JSON only | `BackBackgroundColor`, `BackAccentColor`, `BackBorderColor`, `BackBackgroundImage`, `BackBackgroundImageFit` | Card back tab | white, navy, gray, none, Contain | Back appearance |

`--exclude-redeemed` and `--exclude-unavailable` override the corresponding included filters in a CLI command.

## Desktop preferences and current limits

Desktop only — does not change the exported PDF. Theme and preview zoom are saved separately at `%APPDATA%/CouponSheetGenerator/desktop-preferences.json` on Windows (the corresponding application-data directory on other platforms); this file contains no imported records or sensitive values. Window placement and recent-file preferences are not persisted. Preview PDFs are temporary and removed on normal shutdown; deletion does not guarantee secure erasure. Exported PDFs contain redeemable information. Local PNG/JPEG assets are limited to 10 MiB and 4096 × 4096 pixels. The desktop currently has no manual selection, live preview, configurable date formats, unrestricted canvas, or native printing. The embedded preview renders one PDF page at a time. The CLI still uses its established warnings for invalid optional booleans and dates. There is no GUI smoke-test automation yet.

## Troubleshooting

- “Missing required headers”: check header spelling and tabs, and ensure the first row is the header.
- “No eligible coupons”: inspect the validation summary and date/status fields; use inclusion flags only when intended.
- “Cards are too small”: reduce rows/columns or margins.
- PDF looks clipped when printed: select actual size / 100% and check printer margins.
- A build cannot reach NuGet: restore packages while connected, then use `--no-restore` for subsequent build/test commands.
