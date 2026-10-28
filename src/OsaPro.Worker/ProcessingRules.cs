using System.Globalization;
using OsaPro.NAV;

namespace OsaPro.Worker;

internal readonly record struct InvoiceIdentity(string TaxNumber, string InvoiceNumber)
{
    public static InvoiceIdentity Create(string? taxNumber, string? invoiceNumber)
    {
        return new InvoiceIdentity((taxNumber ?? "").Trim(), (invoiceNumber ?? "").Trim());
    }
}

internal static class ProcessingSlices
{
    public const int MaxDays = 35;

    public static IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> ForRun(
        DateOnly startDate,
        DateTimeOffset storedInsDate,
        DateTimeOffset now)
    {
        var (from, to) = NavTimestamps.Window(startDate, storedInsDate, now);
        var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (from < start)
        {
            throw new ProcessingException("A szelet a kezdődátum előtt indulna.");
        }

        return Cut(from, to);
    }

    public static IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> Cut(DateTimeOffset from, DateTimeOffset to)
    {
        var slices = new List<(DateTimeOffset From, DateTimeOffset To)>();
        if (from >= to)
        {
            return slices;
        }

        var cursor = from;
        while (cursor < to)
        {
            var sliceTo = cursor.AddDays(MaxDays);
            if (sliceTo > to)
            {
                sliceTo = to;
            }

            slices.Add((cursor, sliceTo));
            if (sliceTo >= to)
            {
                break;
            }

            cursor = sliceTo;
        }

        return slices;
    }
}

internal sealed record InvoiceRowDraft(
    string Partner,
    string TaxNumber,
    string BankAccount,
    string InvoiceNumber,
    string OriginalInvoice,
    string Related,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal? Gross,
    string VatContent,
    decimal? VatAmount,
    string PaymentMethod,
    string InvoicePresent,
    string Paid,
    string CarOrder,
    string Note);

internal static class InvoiceRows
{
    public static string TaxNumber(string? supplierTaxNumber)
    {
        var digits = new string((supplierTaxNumber ?? "").Trim().Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? digits[..8] : digits;
    }

    public static string PaymentLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        var trimmed = code.Trim();
        return trimmed switch
        {
            "TRANSFER" => "Átutalás",
            "CASH" => "Készpénz",
            "CARD" => "Bankkártya",
            "VOUCHER" => "Utalvány",
            "OTHER" => "Egyéb",
            _ => trimmed
        };
    }

    public static InvoiceRowDraft Build(
        string? partner,
        string? supplierTaxNumber,
        string? invoiceNumber,
        string? originalInvoice,
        DateOnly issueDate,
        DateOnly? paymentDate,
        decimal? gross,
        decimal? vatAmount,
        string? bankAccount,
        string? paymentMethod,
        string vatContent)
    {
        return new InvoiceRowDraft(
            partner ?? "",
            TaxNumber(supplierTaxNumber),
            string.IsNullOrWhiteSpace(bankAccount) ? "" : bankAccount,
            (invoiceNumber ?? "").Trim(),
            string.IsNullOrWhiteSpace(originalInvoice) ? "" : originalInvoice.Trim(),
            "",
            issueDate,
            paymentDate ?? issueDate,
            gross,
            vatContent,
            vatAmount,
            PaymentLabel(paymentMethod),
            "",
            "",
            "",
            "");
    }
}

internal enum VatKind
{
    Percentage,
    Content,
    Exemption,
    OutOfScope,
    Mismatch,
    ReverseCharge,
    Margin,
    NoCharge
}

internal readonly record struct VatMarker(VatKind Kind, decimal Number, string Code)
{
    public static VatMarker Percentage(decimal value) => new(VatKind.Percentage, value, "");

    public static VatMarker Content(decimal value) => new(VatKind.Content, value, "");

    public static VatMarker Exemption(string code) => new(VatKind.Exemption, 0, code);

    public static VatMarker OutOfScope(string code) => new(VatKind.OutOfScope, 0, code);

    public static VatMarker Mismatch(string code) => new(VatKind.Mismatch, 0, code);

    public static VatMarker ReverseCharge() => new(VatKind.ReverseCharge, 0, "FAD");

    public static VatMarker Margin(string code) => new(VatKind.Margin, 0, code);

    public static VatMarker NoCharge() => new(VatKind.NoCharge, 0, "noVatCharge");
}

internal static class VatContentText
{
    public static string Join(IReadOnlyList<VatMarker> markers, bool simplified)
    {
        var tokens = new List<string>();
        foreach (var marker in markers)
        {
            var token = Token(marker, simplified);
            if (token.Length == 0 || tokens.Contains(token, StringComparer.Ordinal))
            {
                continue;
            }

            tokens.Add(token);
        }

        return string.Join(",", tokens);
    }

    private static string Token(VatMarker marker, bool simplified)
    {
        return marker.Kind switch
        {
            VatKind.Exemption or VatKind.OutOfScope or VatKind.Mismatch or VatKind.Margin => marker.Code,
            VatKind.ReverseCharge => "FAD",
            VatKind.NoCharge => "noVatCharge",
            VatKind.Percentage => Percent(marker.Number),
            VatKind.Content => simplified ? Simplified(marker.Number) : "",
            _ => ""
        };
    }

    private static string Simplified(decimal content)
    {
        if (content == 0.2126m)
        {
            return "27%";
        }

        if (content == 0.1525m)
        {
            return "18%";
        }

        if (content == 0.0426m)
        {
            return "5%";
        }

        if (content == 0m)
        {
            return "0%";
        }

        return Percent(content);
    }

    private static string Percent(decimal fraction)
    {
        var scale = Math.Max(0, fraction.Scale - 2);
        var percent = decimal.Round(fraction * 100m, scale, MidpointRounding.AwayFromZero);
        return percent.ToString("0.#############################", CultureInfo.InvariantCulture) + "%";
    }
}

internal sealed class TrackedInvoice
{
    public int Year { get; init; }

    public required string TaxNumber { get; init; }

    public required string InvoiceNumber { get; init; }

    public string Related { get; set; } = "";
}

internal sealed class SliceLinkItem
{
    public required string Operation { get; init; }

    public required string TaxNumber { get; init; }

    public required string InvoiceNumber { get; init; }

    public string? OriginalInvoiceNumber { get; init; }
}

internal readonly record struct LinkUpdate(string TaxNumber, string InvoiceNumber, string Related);

internal static class InvoiceLinks
{
    public static (IReadOnlyList<LinkUpdate> Updates, IReadOnlyList<InvoiceIdentity> Missing) Decide(
        IReadOnlyList<TrackedInvoice> registry,
        IReadOnlyList<SliceLinkItem> slice)
    {
        var updates = new List<LinkUpdate>();
        var missing = new List<InvoiceIdentity>();
        foreach (var item in slice)
        {
            if (item.Operation is not ("MODIFY" or "STORNO"))
            {
                continue;
            }

            var originalNumber = (item.OriginalInvoiceNumber ?? "").Trim();
            var original = registry.FirstOrDefault(row =>
                InvoiceIdentity.Create(row.TaxNumber, row.InvoiceNumber)
                == InvoiceIdentity.Create(item.TaxNumber, originalNumber));
            if (original is null)
            {
                missing.Add(InvoiceIdentity.Create(item.TaxNumber, item.InvoiceNumber));
                continue;
            }

            var related = Append(original.Related, item.InvoiceNumber, item.Operation);
            if (!string.Equals(related, original.Related, StringComparison.Ordinal))
            {
                original.Related = related;
                updates.Add(new LinkUpdate(original.TaxNumber, original.InvoiceNumber, related));
            }
        }

        return (updates, missing);
    }

    public static string Append(string? existing, string invoiceNumber, string operation = "MODIFY")
    {
        var number = (invoiceNumber ?? "").Trim();
        var token = operation == "STORNO" ? "STORNO:" + number : number;
        var current = existing ?? "";
        foreach (var part in current.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (SameNumber(part, number))
            {
                return current;
            }
        }

        return current.Length == 0 ? token : current + ";" + token;
    }

    private static bool SameNumber(string token, string number)
    {
        var value = token.StartsWith("STORNO:", StringComparison.Ordinal) ? token["STORNO:".Length..] : token;
        return string.Equals(value, number, StringComparison.Ordinal);
    }
}
