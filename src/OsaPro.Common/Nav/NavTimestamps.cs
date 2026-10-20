using System.Globalization;

namespace OsaPro.Common.Nav;

internal static class NavTimestamps
{
    public static string Format(DateTimeOffset value)
    {
        return value.ToUniversalTime().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    public static string SignatureMask(DateTimeOffset value)
    {
        return value.ToUniversalTime().UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }

    public static (DateTimeOffset From, DateTimeOffset To) Window(DateOnly startDate, DateTimeOffset storedInsDate, DateTimeOffset now)
    {
        var from = UtcMidnight(storedInsDate);
        var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (from < start)
        {
            from = start;
        }

        var to = UtcMidnight(now).AddDays(1);
        return (from, to);
    }

    public static IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> Slices(DateTimeOffset from, DateTimeOffset to)
    {
        var slices = new List<(DateTimeOffset From, DateTimeOffset To)>();
        if (from >= to)
        {
            return slices;
        }

        var cursor = from;
        while (cursor < to)
        {
            var sliceTo = cursor.AddDays(35);
            if (sliceTo > to)
            {
                sliceTo = to;
            }

            slices.Add((cursor, sliceTo));
            if (sliceTo >= to)
            {
                break;
            }

            var next = sliceTo.AddMilliseconds(1);
            if (next >= to)
            {
                slices.Add((to, to));
                break;
            }

            cursor = next;
        }

        return slices;
    }

    private static DateTimeOffset UtcMidnight(DateTimeOffset value)
    {
        return new DateTimeOffset(value.ToUniversalTime().UtcDateTime.Date, TimeSpan.Zero);
    }
}
