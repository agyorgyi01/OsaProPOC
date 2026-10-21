namespace OsaPro.Excel;

internal static class WorkbookRules
{
    public const string ParametersSheet = "Paraméterek";

    public const string InsDateKey = "InsDate";

    public const string NoticeDaysKey = "Előértesítési napok";

    public const string DefaultNoticeDays = "7,5,1";

    public static readonly string[] MonthNames =
    [
        "Január",
        "Február",
        "Március",
        "Április",
        "Május",
        "Június",
        "Július",
        "Augusztus",
        "Szeptember",
        "Október",
        "November",
        "December"
    ];

    public static readonly string[] Columns =
    [
        "Partner neve",
        "Adószám",
        "Bankszámlaszám",
        "Számlasorszám",
        "Eredeti számla",
        "Kapcsolt bizonylatok",
        "Számla kelte",
        "Fiz. határidő",
        "Fiz. összeg",
        "Áfa tartalom",
        "Áfa összege",
        "Fizetési mód",
        "Számla megvan?",
        "Fizetve?",
        "Autó rendelés",
        "Megjegyzés"
    ];

    public static string FileName(int year)
    {
        return $"SZÁMLÁK{year}.xlsx";
    }

    public static string MonthName(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ExcelWorkbookException("A hónap 1 és 12 között lehet.");
        }

        return MonthNames[month - 1];
    }

    public static bool IsHandledYear(int year, DateTimeOffset clock)
    {
        var current = CurrentYear(clock);
        return year == current || year == current - 1;
    }

    public static int CurrentYear(DateTimeOffset clock)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        return TimeZoneInfo.ConvertTime(clock, zone).Year;
    }

    public static string DefaultInsDate(int year)
    {
        return $"{year - 1}-12-31T00:00:00Z";
    }

    public static string Identity(string? value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }

    public static bool SameName(string? left, string? right)
    {
        return string.Equals(Identity(left), Identity(right), StringComparison.Ordinal);
    }

    public static string? FindMatch(IEnumerable<string> names, string canonical)
    {
        foreach (var name in names)
        {
            if (SameName(name, canonical))
            {
                return name;
            }
        }

        return null;
    }

    public static string? CopyValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim();
    }

    public static (string InsDate, string NoticeDays) ResolveParameters(
        IReadOnlyList<(string Key, string Value)> rows,
        int year)
    {
        string? insDate = null;
        string? noticeDays = null;
        var insDateSeen = false;
        var noticeDaysSeen = false;
        foreach (var (key, value) in rows)
        {
            if (!insDateSeen && SameName(key, InsDateKey))
            {
                insDateSeen = true;
                insDate = CopyValue(value);
            }

            if (!noticeDaysSeen && SameName(key, NoticeDaysKey))
            {
                noticeDaysSeen = true;
                noticeDays = CopyValue(value);
            }
        }

        return (insDate ?? DefaultInsDate(year), noticeDays ?? DefaultNoticeDays);
    }

    public static IReadOnlyList<ColumnInsert> PlanColumnInserts(IReadOnlyList<string> headers)
    {
        var cells = headers.ToList();
        var inserts = new List<ColumnInsert>();
        var found = CanonicalIndexes(cells);
        if (found.Count == 0)
        {
            for (var index = 0; index < Columns.Length; index++)
            {
                inserts.Add(new ColumnInsert(index, Columns[index]));
                cells.Insert(index, Columns[index]);
            }

            return inserts;
        }

        foreach (var name in Columns)
        {
            if (found.ContainsKey(name))
            {
                continue;
            }

            var insertAt = InsertionIndex(cells, name);
            inserts.Add(new ColumnInsert(insertAt, name));
            cells.Insert(insertAt, name);
        }

        return inserts;
    }

    public static void RequireAddress(ExcelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SiteHost)
            || string.IsNullOrWhiteSpace(options.SitePath)
            || string.IsNullOrWhiteSpace(options.FolderPath))
        {
            throw new ExcelWorkbookException("Az Excel cím mezői nincsenek kitöltve.");
        }
    }

    public static void RequireMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ExcelWorkbookException("A hónap 1 és 12 között lehet.");
        }
    }

    public static void RequireHandledYear(int year, DateTimeOffset clock)
    {
        if (!IsHandledYear(year, clock))
        {
            throw new ExcelWorkbookException("A kért év nincs a kezelt két év között.");
        }
    }

    private static Dictionary<string, int> CanonicalIndexes(IReadOnlyList<string> headers)
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            foreach (var name in Columns)
            {
                if (found.ContainsKey(name))
                {
                    continue;
                }

                if (SameName(headers[index], name))
                {
                    found[name] = index;
                }
            }
        }

        return found;
    }

    private static int InsertionIndex(IReadOnlyList<string> headers, string missing)
    {
        var order = Array.IndexOf(Columns, missing);
        for (var next = order + 1; next < Columns.Length; next++)
        {
            for (var index = 0; index < headers.Count; index++)
            {
                if (SameName(headers[index], Columns[next]))
                {
                    return index;
                }
            }
        }

        var last = -1;
        for (var index = 0; index < headers.Count; index++)
        {
            foreach (var name in Columns)
            {
                if (SameName(headers[index], name))
                {
                    last = index;
                }
            }
        }

        return last + 1;
    }
}

internal readonly record struct ColumnInsert(int Index, string Name);
