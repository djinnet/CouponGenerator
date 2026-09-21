using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace CouponSheetGenerator;

public sealed record CouponRecord(int Row, string Product, string Code, string Url, DateOnly? Start, DateOnly? Expiry, bool Available, bool Redeemed, string GivenTo, string CodeId, string OrderId, string OrderName = "");
public sealed record ReadResult(List<CouponRecord> Records, List<string> Warnings, int RowsRead, int InvalidRows);
public sealed record Selection(List<CouponRecord> Coupons, int RedeemedExcluded, int UnavailableExcluded, int ExpiredExcluded, int FutureExcluded);

public static class TsvReader
{
    static readonly string[] Required = ["Product name", "Promotional code", "Redeemable URL"];
    static readonly string[] Optional = ["Order name", "Start date", "Expire date", "Code ID", "Order ID", "Given to", "Available", "Redeemed"];
    public static ReadResult Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Input TSV file not found.", path);
        }

        using var reader = new StreamReader(path, System.Text.Encoding.UTF8, true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.None,
            BadDataFound = bad => throw new InvalidOperationException($"Invalid TSV structure near row {bad.Context.Parser?.Row ?? 0}."),
            MissingFieldFound = null,
            HeaderValidated = null
        });
        if (!csv.Read())
        {
            throw new InvalidOperationException("TSV is empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var key = headers[i].Trim().TrimStart('\uFEFF');
            if (!map.TryAdd(key, i))
            {
                throw new InvalidOperationException($"Duplicate TSV header: {key}");
            }
        }

        if (new FileInfo(path).Length > 32 * 1024 * 1024)
        {
            throw new InvalidOperationException("TSV exceeds the 32 MiB input limit.");
        }

        var missing = Required.Where(h => !map.ContainsKey(h)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Missing required headers: {string.Join(", ", missing)}");
        }

        var records = new List<CouponRecord>();
        var warnings = new List<string>();
        foreach (var optional in Optional.Where(h => !map.ContainsKey(h)))
        {
            warnings.Add($"Optional column absent: {optional}.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        var codeIds = new HashSet<string>(StringComparer.Ordinal);
        int rows = 0, invalid = 0;
        while (csv.Read())
        {
            rows++;
            if (rows > 100_000)
            {
                throw new InvalidOperationException("TSV exceeds the 100,000-record limit.");
            }

            int row = csv.Parser?.Row ?? rows + 1;
            if (csv.Parser?.Count != headers.Length)
            {
                invalid++; warnings.Add($"Row {row}: incorrect field count; skipped."); continue;
            }
            string Field(string name) => map.TryGetValue(name, out int i) && i < csv.Parser.Count ? csv.GetField(i) ?? "" : "";
            string product = Field("Product name").Trim();
            string code = Field("Promotional code");
            string url = Field("Redeemable URL").Trim();
            if (Enumerable.Range(0, csv.Parser.Count).Any(i => (csv.GetField(i)?.Length ?? 0) > 16_384))
            {
                invalid++; warnings.Add($"Row {row}: field exceeds the 16,384-character limit; skipped."); continue;
            }
            if (string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(code))
            {
                invalid++; warnings.Add($"Row {row}: missing product/code; skipped."); continue;
            }

            // Redeemable URLs are encoded locally in QR codes; accept absolute HTTP or HTTPS URLs.
            var urlValid = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrWhiteSpace(uriResult.Host);
            if (!urlValid)
            {
                invalid++; warnings.Add($"Row {row}: invalid URL; skipped."); continue;
            }

            if (!codes.Add(code))
            {
                warnings.Add($"Row {row}: duplicate promotional code; retained as a separate record.");
            }

            var codeId = Field("Code ID").Trim();
            if (codeId.Length > 0 && !codeIds.Add(codeId))
            {
                warnings.Add($"Row {row}: duplicate Code ID; retained as a separate record.");
            }

            bool available = ParseBool(Field("Available"), true, "Available", row, warnings);
            bool redeemed = ParseBool(Field("Redeemed"), false, "Redeemed", row, warnings);
            DateOnly? start = ParseDate(Field("Start date"), "Start date", row, warnings);
            DateOnly? expiry = ParseDate(Field("Expire date"), "Expire date", row, warnings);
            records.Add(new(row, product, code, url, start, expiry, available, redeemed, Field("Given to").Trim(), codeId, Field("Order ID").Trim(), Field("Order name").Trim()));
        }
        if (rows == 0)
        {
            warnings.Add("TSV contains a header but no records.");
        }

        return new(records, warnings, rows, invalid);
    }
    static bool ParseBool(string raw, bool fallback, string name, int row, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "1" => true,
            "false" or "no" or "0" => false,
            _ => Warn()
        };
        bool Warn()
        {
            warnings.Add($"Row {row}: invalid {name}; using {fallback}.");
            return fallback;
        }
    }
    static DateOnly? ParseDate(string raw, string name, int row, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string[] formats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd MMM yyyy", "d MMM yyyy", "MMM d yyyy", "MMMM d yyyy"];
        if (DateOnly.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        warnings.Add($"Row {row}: invalid or ambiguous {name}; date ignored.");
        return null;
    }
}

public static class Eligibility
{
    public static Selection Select(IEnumerable<CouponRecord> records, Options o, DateOnly today)
    {
        var coupons = new List<CouponRecord>();
        int redeemed = 0, unavailable = 0, expired = 0, future = 0;
        foreach (var r in records)
        {
            if (r.Redeemed && !o.IncludeRedeemed) { redeemed++; continue; }
            if (!r.Available && !o.IncludeUnavailable) { unavailable++; continue; }
            if (r.Expiry < today && !o.IncludeExpired) { expired++; continue; }
            if (r.Start > today && !o.IncludeFuture) { future++; continue; }
            coupons.Add(r);
        }
        return new(coupons, redeemed, unavailable, expired, future);
    }
}
