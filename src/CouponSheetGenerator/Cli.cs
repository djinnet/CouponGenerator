using System.Globalization;
using System.Text.Json;

namespace CouponSheetGenerator;

public static class Cli
{
    public const string Usage = """
    Usage:
      CouponSheetGenerator validate --input FILE [--config FILE]
      CouponSheetGenerator generate --input FILE --output FILE [options]
    Options:
      --page-size A4|Letter --orientation portrait|landscape
      --columns N --rows N --margin MM --card-gap MM
      --cut-marks --page-numbers --logo FILE --title TEXT --footer TEXT
      --background-color #RRGGBB --accent-color #RRGGBB --black-and-white
      --include-given-to --include-url none|short|full --include-ids
      --include-redeemed --include-unavailable --include-expired --include-future
      --exclude-redeemed --exclude-unavailable --config FILE --verbose
    """;
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cut-marks",
            "page-numbers",
            "include-given-to",
            "include-ids",
            "include-redeemed",
            "include-unavailable",
            "include-expired",
            "include-future",
            "exclude-redeemed",
            "exclude-unavailable",
            "black-and-white",
            "verbose"
        };
        var known = new HashSet<string>(flags, StringComparer.OrdinalIgnoreCase);
        known.UnionWith(["input", "output", "page-size", "orientation", "columns", "rows", "margin", "card-gap", "logo", "title", "footer", "background-color", "accent-color", "include-url", "config"]);
        for (int i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
            {
                throw new ArgumentException($"Unexpected argument: {args[i]}\n{Usage}");
            }

            string name = args[i][2..];
            if (!known.Contains(name))
            {
                throw new ArgumentException($"Unknown option: --{name}\n{Usage}");
            }

            values[name] = flags.Contains(name) ? "true" : ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for --{name}");
        }
        Options o = new();
        if (values.TryGetValue("config", out var config))
        {
            o = JsonSerializer.Deserialize<Options>(File.ReadAllText(config!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        string? Values(string key) => values.GetValueOrDefault(key);
        if (Values("input") is { } input)
        {
            o.Input = input;
        }

        if (Values("output") is { } output)
        {
            o.Output = output;
        }

        if (Values("page-size") is { } size)
        {
            o.PageSize = size;
        }

        if (Values("orientation") is { } orientation)
        {
            o.Orientation = orientation;
        }

        if (Values("columns") is { } cols)
        {
            o.Columns = int.Parse(cols, CultureInfo.InvariantCulture);
        }

        if (Values("rows") is { } rows)
        {
            o.Rows = int.Parse(rows, CultureInfo.InvariantCulture);
        }

        if (Values("margin") is { } margin)
        {
            o.Margin = double.Parse(margin, CultureInfo.InvariantCulture);
        }

        if (Values("card-gap") is { } gap)
        {
            o.CardGap = double.Parse(gap, CultureInfo.InvariantCulture);
        }

        if (Values("logo") is { } logo)
        {
            o.Logo = logo;
        }

        if (Values("title") is { } title)
        {
            o.Title = title;
        }

        if (Values("footer") is { } footer)
        {
            o.Footer = footer;
        }

        if (Values("background-color") is { } background)
        {
            o.BackgroundColor = background;
        }

        if (Values("accent-color") is { } accent)
        {
            o.AccentColor = accent;
        }

        if (Values("include-url") is { } url)
        {
            o.IncludeUrl = url;
        }

        foreach (var (key, setter) in new (string, Action<bool>)[]
        {
            ("cut-marks", x => o.CutMarks=x), ("page-numbers", x => o.PageNumbers=x),
            ("include-given-to", x => o.IncludeGivenTo=x), ("include-ids", x => o.IncludeIds=x),
            ("include-redeemed", x => o.IncludeRedeemed=x), ("include-unavailable", x => o.IncludeUnavailable=x),
            ("include-expired", x => o.IncludeExpired=x), ("include-future", x => o.IncludeFuture=x),
            ("black-and-white", x => o.BlackAndWhite=x), ("verbose", x => o.Verbose=x)
        })
            if (Values(key) is not null)
            {
                setter(true);
            }

        if (Values("exclude-redeemed") is not null)
        {
            o.IncludeRedeemed = false;
        }

        if (Values("exclude-unavailable") is not null)
        {
            o.IncludeUnavailable = false;
        }

        if (string.IsNullOrWhiteSpace(o.Input))
        {
            throw new ArgumentException($"--input is required.\n{Usage}");
        }

        if (args[0] == "generate" && string.IsNullOrWhiteSpace(o.Output))
        {
            throw new ArgumentException($"--output is required.\n{Usage}");
        }

        if (o.Columns < 1 || o.Columns > 10 || o.Rows < 1 || o.Rows > 10 || o.Margin < 0 || o.CardGap < 0)
        {
            throw new ArgumentException("Invalid grid or spacing value.");
        }

        if (o.PageSize is not ("A4" or "Letter") || o.Orientation is not ("portrait" or "landscape") || o.IncludeUrl is not ("none" or "short" or "full"))
        {
            throw new ArgumentException("Invalid page size, orientation, or URL display mode.");
        }

        foreach (var color in new[] { o.BackgroundColor, o.AccentColor })
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
            {
                throw new ArgumentException("Colors must be #RRGGBB.");
            }
        }

        return o;
    }
}
