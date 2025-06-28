using System.Globalization;

namespace Lfm.Core.Services;

public static class DateRangeParser
{
    public static (DateTime from, DateTime to) ParseDateRange(string? fromStr, string? toStr)
    {
        if (string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(toStr))
        {
            throw new ArgumentException("Both --from and --to must be specified when using custom date ranges");
        }

        var fromDate = ParseSingleDate(fromStr, isStartDate: true);
        var toDate = ParseSingleDate(toStr, isStartDate: false);

        if (fromDate >= toDate)
        {
            throw new ArgumentException("From date must be before to date");
        }

        return (fromDate, toDate);
    }

    public static (DateTime from, DateTime to) ParseYearRange(string yearStr)
    {
        if (string.IsNullOrWhiteSpace(yearStr))
        {
            throw new ArgumentException("Year cannot be empty");
        }

        if (!int.TryParse(yearStr, out var year))
        {
            throw new ArgumentException($"Invalid year format: '{yearStr}'. Use YYYY format (e.g., 2017)");
        }

        if (year < 1900 || year > DateTime.Now.Year + 1)
        {
            throw new ArgumentException($"Year {year} is out of valid range (1900 - {DateTime.Now.Year + 1})");
        }

        var fromDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        return (fromDate, toDate);
    }

    public static DateTime ParseSingleDate(string dateStr, bool isStartDate)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
        {
            throw new ArgumentException("Date string cannot be empty");
        }

        // Year shortcut: "2017" -> "2017-01-01" or "2017-12-31"
        if (int.TryParse(dateStr, out var year))
        {
            if (year < 1900 || year > DateTime.Now.Year + 1)
            {
                throw new ArgumentException($"Year {year} is out of valid range (1900 - {DateTime.Now.Year + 1})");
            }

            return isStartDate 
                ? new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                : new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        }

        // Full date format: "2017-01-01" or "2017-12-31"
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fullDate))
        {
            return isStartDate
                ? fullDate.Date
                : fullDate.Date.AddDays(1).AddSeconds(-1); // End of day
        }

        throw new ArgumentException($"Invalid date format: '{dateStr}'. Use YYYY-MM-DD or YYYY (year shortcut)");
    }

    public static long ToUnixTimestamp(DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime.ToUniversalTime()).ToUnixTimeSeconds();
    }

    public static string FormatDateRange(DateTime from, DateTime to)
    {
        return $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";
    }

    public static bool IsYearShortcut(string dateStr)
    {
        return int.TryParse(dateStr, out var year) && year >= 1900 && year <= DateTime.Now.Year + 1;
    }

    public static void ValidateDateRangeParameters(string? periodStr, string? fromStr, string? toStr, string? yearStr)
    {
        // Count explicitly provided parameters
        var providedParams = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(periodStr))
            providedParams.Add("--period");
            
        if (!string.IsNullOrWhiteSpace(fromStr) || !string.IsNullOrWhiteSpace(toStr))
            providedParams.Add("--from/--to");
            
        if (!string.IsNullOrWhiteSpace(yearStr))
            providedParams.Add("--year");

        if (providedParams.Count > 1)
        {
            throw new ArgumentException($"Cannot specify multiple time parameters: {string.Join(", ", providedParams)}. Use either --period, --from/--to, or --year");
        }

        if (!string.IsNullOrWhiteSpace(fromStr) && string.IsNullOrWhiteSpace(toStr))
        {
            throw new ArgumentException("When using --from, you must also specify --to");
        }

        if (string.IsNullOrWhiteSpace(fromStr) && !string.IsNullOrWhiteSpace(toStr))
        {
            throw new ArgumentException("When using --to, you must also specify --from");
        }
    }
}