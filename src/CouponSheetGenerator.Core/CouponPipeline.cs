namespace CouponSheetGenerator;

public interface ICouponPipeline
{
    ReadResult Read(string path);
    Selection Select(IEnumerable<CouponRecord> records, Options settings, DateTime asOf);
    int WritePdf(IReadOnlyList<CouponRecord> records, Options settings);
}

public sealed class CouponPipeline : ICouponPipeline
{
    public ReadResult Read(string path) => TsvReader.Read(path);
    public Selection Select(IEnumerable<CouponRecord> records, Options settings, DateTime asOf) => Eligibility.Select(records, settings, asOf);
    public int WritePdf(IReadOnlyList<CouponRecord> records, Options settings) => PdfWriter.Write(records, settings);
}
