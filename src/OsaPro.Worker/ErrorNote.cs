namespace OsaPro.Worker;

internal sealed class ErrorNote
{
    public required string Identity { get; init; }

    public required string Kind { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyDictionary<string, string> Payload { get; init; }
}

internal interface IErrorLog
{
    ErrorNote? Find(string identity);

    void Add(ErrorNote note);
}

internal sealed class MemoryErrorLog : IErrorLog
{
    private readonly Dictionary<string, ErrorNote> _notes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ErrorNote> All => _notes.Values;

    public ErrorNote? Find(string identity)
    {
        return _notes.GetValueOrDefault(identity);
    }

    public void Add(ErrorNote note)
    {
        if (_notes.ContainsKey(note.Identity))
        {
            return;
        }

        _notes[note.Identity] = note;
    }
}

internal static class ErrorRecords
{
    public const string OutOfYear = "out-of-year";

    public const string MissingOriginal = "missing-original";

    public const string Cycle = "cycle";

    public static readonly string[] ForbiddenFields =
    [
        "SendStatus",
        "RetryCount",
        "Retry",
        "Text",
        "Channel",
        "NotificationText",
        "Küldési állapot",
        "Újrapróbálás",
        "Szöveg",
        "Csatorna"
    ];

    public static ErrorNote OutOfYearNote(
        InvoiceIdentity identity,
        string partner,
        DateOnly issueDate,
        DateOnly deadline,
        decimal? payable)
    {
        return new ErrorNote
        {
            Identity = Key(OutOfYear, identity.TaxNumber, identity.InvoiceNumber),
            Kind = OutOfYear,
            Reason = "A kelte a kezelt éveken kívül van.",
            Payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PartnerName"] = partner,
                ["TaxNumber"] = identity.TaxNumber,
                ["InvoiceNumber"] = identity.InvoiceNumber,
                ["IssueDate"] = issueDate.ToString("yyyy-MM-dd"),
                ["PayableAmount"] = Amount(payable),
                ["Deadline"] = deadline.ToString("yyyy-MM-dd")
            }
        };
    }

    public static ErrorNote MissingOriginalNote(
        InvoiceIdentity identity,
        string partner,
        DateOnly issueDate,
        DateOnly deadline,
        decimal? payable,
        string originalInvoiceNumber)
    {
        return new ErrorNote
        {
            Identity = Key(MissingOriginal, identity.TaxNumber, identity.InvoiceNumber),
            Kind = MissingOriginal,
            Reason = "Az eredeti nincs a nyilvántartásban.",
            Payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PartnerName"] = partner,
                ["TaxNumber"] = identity.TaxNumber,
                ["InvoiceNumber"] = identity.InvoiceNumber,
                ["IssueDate"] = issueDate.ToString("yyyy-MM-dd"),
                ["PayableAmount"] = Amount(payable),
                ["Deadline"] = deadline.ToString("yyyy-MM-dd"),
                ["OriginalInvoiceNumber"] = originalInvoiceNumber
            }
        };
    }

    public static ErrorNote CycleNote(DateTimeOffset from, DateTimeOffset to, string stage)
    {
        var fromText = Timestamp(from);
        var toText = Timestamp(to);
        return new ErrorNote
        {
            Identity = Key(Cycle, fromText, toText, stage),
            Kind = Cycle,
            Reason = "A ciklus hibával leállt, a feldolgozás megállt.",
            Payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SliceFrom"] = fromText,
                ["SliceTo"] = toText,
                ["Stage"] = stage
            }
        };
    }

    public static ErrorNote CursorNote()
    {
        return new ErrorNote
        {
            Identity = Key(Cycle, "cursor", "excel"),
            Kind = Cycle,
            Reason = "A ciklus hibával leállt, a feldolgozás megállt.",
            Payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SliceFrom"] = "",
                ["SliceTo"] = "",
                ["Stage"] = "excel"
            }
        };
    }

    public static IReadOnlyDictionary<string, string> TableFields(ErrorNote note)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PartitionKey"] = note.Kind,
            ["RowKey"] = note.Identity,
            ["Reason"] = note.Reason
        };
        foreach (var pair in note.Payload)
        {
            fields[pair.Key] = pair.Value;
        }

        return fields;
    }

    private static string Amount(decimal? value)
    {
        return value is decimal amount
            ? amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "";
    }

    private static string Timestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Key(params string[] parts)
    {
        return string.Join("|", parts);
    }
}
