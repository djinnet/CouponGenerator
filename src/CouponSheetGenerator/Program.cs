using CouponSheetGenerator;

try
{
    if (args.Length == 0 || args[0] is not ("generate" or "validate"))
    {
        Console.WriteLine(Cli.Usage);
        return args.Length == 0 ? 0 : 2;
    }
    var options = Cli.Parse(args);
    var result = TsvReader.Read(options.Input!);
    foreach (var warning in result.Warnings)
    {
        Console.Error.WriteLine(warning);
    }

    var selection = Eligibility.Select(result.Records, options, DateTime.Now);
    if (args[0] == "validate")
    {
        Console.WriteLine($"Rows read: {result.RowsRead}; valid records: {result.Records.Count}; invalid records: {result.InvalidRows}; eligible coupons: {selection.Coupons.Count}");
        return result.InvalidRows > 0 ? 1 : 0;
    }
    if (selection.Coupons.Count == 0)
    {
        throw new InvalidOperationException("No eligible coupons found.");
    }

    var pages = PdfWriter.Write(selection.Coupons, options);
    Console.WriteLine($"Rows read: {result.RowsRead}{Environment.NewLine}Coupons generated: {selection.Coupons.Count}{Environment.NewLine}Rows skipped: {result.RowsRead - selection.Coupons.Count}{Environment.NewLine}Invalid records: {result.InvalidRows}{Environment.NewLine}Redeemed excluded: {selection.RedeemedExcluded}{Environment.NewLine}Unavailable excluded: {selection.UnavailableExcluded}{Environment.NewLine}Expired excluded: {selection.ExpiredExcluded}{Environment.NewLine}Future excluded: {selection.FutureExcluded}{Environment.NewLine}PDF pages: {pages}{Environment.NewLine}Output: {Path.GetFullPath(options.Output!)}");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

