using System.Globalization;

namespace SusamUretim.Web.Services;

public static class ExcelExportRouting
{
    // Dosya yılı kaydın takvim yılıdır; hafta sekmesi ISO hafta numarasını kullanmaya devam eder.
    public static int Year(DateTime date) => date.Year;

    public static string ResolveYearPath(string pattern, DateTime date)
    {
        var year = Year(date);
        return pattern.Contains("{year}", StringComparison.OrdinalIgnoreCase)
            ? pattern.Replace("{year}", year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            : pattern;
    }

    public static bool IsSingleYear(DateTime from, DateTime to) => Year(from) == Year(to);
}
