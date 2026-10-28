using System.Globalization;
using OsaPro.Excel;

namespace OsaPro.Worker;

internal sealed class NoticeInvoice
{
    public string Partner { get; init; } = "";

    public string TaxNumber { get; init; } = "";

    public string InvoiceNumber { get; init; } = "";

    public string OriginalInvoice { get; init; } = "";

    public string Related { get; init; } = "";

    public string IssueDate { get; init; } = "";

    public string Deadline { get; init; } = "";

    public string Amount { get; init; } = "";

    public string PaymentMethod { get; init; } = "";

    public string Present { get; init; } = "";

    public string Paid { get; init; } = "";
}

internal static class NoticeRules
{
    private static readonly DateOnly ExcelEpoch = new(1899, 12, 30);

    public static IReadOnlyList<NotificationNote> Compose(
        DateTimeOffset now,
        IReadOnlyList<NoticeInvoice> invoices,
        string? noticeDays)
    {
        var today = WorkbookRules.CurrentDate(now);
        var weekend = today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var notes = new List<NotificationNote>();
        var noticeValid = TryNotice(noticeDays, out var offsets, out var shown);
        if (!noticeValid)
        {
            notes.Add(NotificationRecords.NoticeDaysNote(shown));
        }

        var blocked = StornoChain(invoices);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var invoice in invoices)
        {
            var tax = Trim(invoice.TaxNumber);
            var number = Trim(invoice.InvoiceNumber);
            if (!seen.Add(tax + "|" + number))
            {
                continue;
            }

            var paidRaw = invoice.Paid ?? "";
            var paid = paidRaw.Trim().ToUpperInvariant();
            if (paid.Length > 0 && !string.Equals(paid, "FIZETVE", StringComparison.Ordinal))
            {
                notes.Add(NotificationRecords.PaidNote(tax, Trim(invoice.Partner), number, paidRaw));
            }

            if (weekend
                || blocked.Contains(tax + "|" + number)
                || !IsTransfer(invoice.PaymentMethod)
                || string.Equals(paid, "FIZETVE", StringComparison.Ordinal))
            {
                continue;
            }

            var due = ParseDate(invoice.Deadline) ?? ParseDate(invoice.IssueDate);
            if (due is null || !IsDue(today, due.Value, noticeValid ? offsets : []))
            {
                continue;
            }

            notes.Add(NotificationRecords.PaymentNote(
                today,
                tax,
                Trim(invoice.Partner),
                number,
                due.Value,
                FormatAmount(invoice.Amount)));
        }

        return notes;
    }

    private static bool TryNotice(string? raw, out IReadOnlyList<int> days, out string shown)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            days = [7, 5, 1];
            shown = "";
            return true;
        }

        shown = raw.Trim();
        var parsed = new List<int>();
        foreach (var part in shown.Split(',', StringSplitOptions.TrimEntries))
        {
            if (part.Length == 0
                || !int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || value < 1)
            {
                days = [];
                return false;
            }

            if (!parsed.Contains(value))
            {
                parsed.Add(value);
            }
        }

        days = parsed;
        return parsed.Count > 0;
    }

    private static HashSet<string> StornoChain(IReadOnlyList<NoticeInvoice> invoices)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var owner in invoices)
        {
            var tokens = Tokens(owner.Related);
            if (!tokens.Any(token => token.StartsWith("STORNO:", StringComparison.Ordinal)))
            {
                continue;
            }

            var tax = Trim(owner.TaxNumber);
            var invoice = Trim(owner.InvoiceNumber);
            blocked.Add(tax + "|" + invoice);
            foreach (var token in tokens)
            {
                blocked.Add(tax + "|" + NumberOf(token));
            }

            foreach (var row in invoices)
            {
                if (string.Equals(Trim(row.TaxNumber), tax, StringComparison.Ordinal)
                    && string.Equals(Trim(row.OriginalInvoice), invoice, StringComparison.Ordinal))
                {
                    blocked.Add(Trim(row.TaxNumber) + "|" + Trim(row.InvoiceNumber));
                }
            }
        }

        return blocked;
    }

    private static bool IsDue(DateOnly today, DateOnly deadline, IReadOnlyList<int> offsets)
    {
        foreach (var offset in offsets)
        {
            if (Shift(deadline.AddDays(-offset)) == today)
            {
                return true;
            }
        }

        return Shift(deadline) == today || today > deadline;
    }

    private static DateOnly Shift(DateOnly date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Saturday => date.AddDays(2),
            DayOfWeek.Sunday => date.AddDays(1),
            _ => date
        };
    }

    private static bool IsTransfer(string? method)
    {
        return string.Equals((method ?? "").Trim(), "Átutalás", StringComparison.Ordinal);
    }

    private static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial)
            && serial == decimal.Truncate(serial)
            && serial >= 0
            && serial <= int.MaxValue)
        {
            return ExcelEpoch.AddDays((int)serial);
        }

        return null;
    }

    private static string FormatAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return "";
        }

        return value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private static IReadOnlyList<string> Tokens(string? related)
    {
        return (related ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NumberOf(string token)
    {
        return token.StartsWith("STORNO:", StringComparison.Ordinal) ? token["STORNO:".Length..] : token;
    }

    private static string Trim(string? value)
    {
        return (value ?? "").Trim();
    }
}
