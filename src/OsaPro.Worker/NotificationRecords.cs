using System.Globalization;
using OsaPro.Excel;

namespace OsaPro.Worker;

internal static class NotificationRecords
{
    public static string DayKey(DateOnly day)
    {
        return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static NotificationNote DocumentNote(string tax, string partner, string invoice, string? original, string operation)
    {
        var own = (invoice ?? "").Trim();
        var source = (original ?? "").Trim();
        var message = operation == "STORNO"
            ? $"{source} sztornózás érkezett, számlaszám: {own}"
            : $"{source} módosítása érkezett, számlaszám: {own}";
        return new NotificationNote
        {
            Identity = $"{(tax ?? "").Trim()}|{own}",
            Type = NotificationNote.DocumentType,
            Message = message,
            Taxnumber = (tax ?? "").Trim(),
            Partnername = (partner ?? "").Trim(),
            Invoice = own
        };
    }

    public static NotificationNote PaymentNote(DateOnly day, string tax, string partner, string invoice, DateOnly deadline, string amount)
    {
        return new NotificationNote
        {
            Identity = $"{DayKey(day)}|{tax}|{invoice}",
            Type = NotificationNote.PaymentType,
            Message = $"{invoice} fizetés esedékes {deadline.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)}, összeg: {amount}",
            Taxnumber = tax,
            Partnername = partner,
            Invoice = invoice
        };
    }

    public static NotificationNote PaidNote(string tax, string partner, string invoice, string value)
    {
        return new NotificationNote
        {
            Identity = $"paid|{tax}|{invoice}",
            Type = NotificationNote.ErrorType,
            Message = $"{invoice} Fizetve? értéke hibás: {value}. Kiegyenlítetlenként kezeljük.",
            Taxnumber = tax,
            Partnername = partner,
            Invoice = invoice
        };
    }

    public static NotificationNote NoticeDaysNote(string value)
    {
        return Empty("notice-days", $"Az Előértesítési napok értéke hibás: {value}.");
    }

    public static NotificationNote MissingFileNote(int year, bool currentYear)
    {
        var file = WorkbookRules.FileName(year);
        var message = currentYear
            ? $"A tárgyévi nyilvántartás hiányzik: {file}. A feldolgozás megállt."
            : $"Az előző évi nyilvántartás hiányzik: {file}. A feldolgozás megállt.";
        return Empty("missing-file|" + file, message);
    }

    public static NotificationNote StartDateNote()
    {
        return Empty("start-date", "A kezdődátum hiányzik vagy hibás.");
    }

    public static NotificationNote ReadErrorNote(DateOnly day, int year)
    {
        return Empty("read|" + DayKey(day), $"A nyilvántartás olvasása megállt: {WorkbookRules.FileName(year)}.");
    }

    public static NotificationNote IdleNote(DateOnly day)
    {
        return new NotificationNote
        {
            Identity = DayKey(day),
            Type = NotificationNote.IdleType,
            Message = "mára nincs teendő, igyál egy kávét"
        };
    }

    private static NotificationNote Empty(string identity, string message)
    {
        return new NotificationNote
        {
            Identity = identity,
            Type = NotificationNote.ErrorType,
            Message = message
        };
    }

    public const string OutOfYear = "out-of-year";

    public const string MissingOriginal = "missing-original";

    public const string Cycle = "cycle";

    public static NotificationNote OutOfYearNote(
        InvoiceIdentity identity,
        string partner,
        DateOnly issueDate,
        DateOnly deadline,
        decimal? payable)
    {
        return Note(
            Key(OutOfYear, identity.TaxNumber, identity.InvoiceNumber),
            "A kelte a kezelt éveken kívül van.",
            partner,
            identity.TaxNumber,
            identity.InvoiceNumber,
            issueDate,
            deadline,
            payable,
            originalInvoiceNumber: null);
    }

    public static NotificationNote MissingOriginalNote(
        InvoiceIdentity identity,
        string partner,
        DateOnly issueDate,
        DateOnly deadline,
        decimal? payable,
        string originalInvoiceNumber)
    {
        return Note(
            Key(MissingOriginal, identity.TaxNumber, identity.InvoiceNumber),
            "Az eredeti nincs a nyilvántartásban.",
            partner,
            identity.TaxNumber,
            identity.InvoiceNumber,
            issueDate,
            deadline,
            payable,
            originalInvoiceNumber);
    }

    public static NotificationNote CycleNote(DateTimeOffset from, DateTimeOffset to, string stage)
    {
        return new NotificationNote
        {
            Identity = Key(Cycle, Timestamp(from), Timestamp(to), stage),
            Type = NotificationNote.ErrorType,
            Message = $"A ciklus hibával leállt, a feldolgozás megállt. Szelet: {Timestamp(from)} - {Timestamp(to)}. Szakasz: {stage}.",
            Taxnumber = "",
            Partnername = "",
            Invoice = ""
        };
    }

    public static NotificationNote CursorNote()
    {
        return new NotificationNote
        {
            Identity = Key(Cycle, "cursor", "excel"),
            Type = NotificationNote.ErrorType,
            Message = "A ciklus hibával leállt, a feldolgozás megállt. Szakasz: excel.",
            Taxnumber = "",
            Partnername = "",
            Invoice = ""
        };
    }

    public static IReadOnlyDictionary<string, string> TableFields(NotificationNote note)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PartitionKey"] = note.Type,
            ["RowKey"] = note.Identity,
            ["Type"] = note.Type,
            ["Message"] = note.Message,
            ["Taxnumber"] = note.Taxnumber,
            ["Partnername"] = note.Partnername,
            ["Invoice"] = note.Invoice
        };
    }

    private static NotificationNote Note(
        string identity,
        string reason,
        string partner,
        string taxNumber,
        string invoiceNumber,
        DateOnly issueDate,
        DateOnly deadline,
        decimal? payable,
        string? originalInvoiceNumber)
    {
        var message = $"{reason} Partner: {partner}. Adószám: {taxNumber}. Számlaszám: {invoiceNumber}. Kelte: {issueDate:yyyy-MM-dd}. Fizetendő: {Amount(payable)}. Határidő: {deadline:yyyy-MM-dd}.";
        if (originalInvoiceNumber is not null)
        {
            message += $" Eredeti számla: {originalInvoiceNumber}.";
        }

        return new NotificationNote
        {
            Identity = identity,
            Type = NotificationNote.ErrorType,
            Message = message,
            Taxnumber = taxNumber,
            Partnername = partner,
            Invoice = invoiceNumber
        };
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
