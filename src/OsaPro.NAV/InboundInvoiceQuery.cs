using System.Globalization;
using System.Xml.Linq;
using InvoiceDocument = OsaPro.NAV.InvoiceData.InvoiceData;

namespace OsaPro.NAV;

public sealed class InboundInvoiceQuery : IDisposable
{
    private readonly NavOptions _options;
    private readonly NavInvoiceSender _sender;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<string> _requestIds;

    public InboundInvoiceQuery(
        NavOptions options,
        HttpMessageHandler handler,
        Func<DateTimeOffset> clock,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<string>? requestIds = null)
    {
        _options = options;
        _clock = clock;
        _requestIds = requestIds ?? NavCrypto.RequestId;
        _sender = new NavInvoiceSender(options, handler, delay ?? ((span, token) => Task.Delay(span, token)));
    }

    public async Task<IReadOnlyList<InvoiceDigestItem>> QueryInvoiceDigest(
        DateOnly startDate,
        DateTimeOffset storedInsDate,
        CancellationToken cancellationToken = default)
    {
        var windowNow = _clock().ToUniversalTime();
        var (from, to) = NavTimestamps.Window(startDate, storedInsDate, windowNow);
        var items = new List<InvoiceDigestItem>();
        foreach (var (sliceFrom, sliceTo) in NavTimestamps.Slices(from, to))
        {
            var page = 1;
            var availablePage = 1;
            while (page <= availablePage)
            {
                var request = NavRequestEnvelope.Digest(_options, _requestIds(), _clock().ToUniversalTime(), page, sliceFrom, sliceTo);
                var response = await _sender.PostAsync("queryInvoiceDigest", request, cancellationToken).ConfigureAwait(false);
                var parsed = ParseDigest(response);
                availablePage = parsed.AvailablePage;
                items.AddRange(parsed.Items);
                page++;
            }
        }

        return items;
    }

    public async Task<InvoiceDocument> QueryInvoiceData(
        string invoiceNumber,
        string supplierTaxNumber,
        int? batchIndex = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = _clock().ToUniversalTime();
        var request = NavRequestEnvelope.Invoice(
            _options,
            _requestIds(),
            timestamp,
            invoiceNumber,
            supplierTaxNumber,
            batchIndex);
        var response = await _sender.PostAsync("queryInvoiceData", request, cancellationToken).ConfigureAwait(false);
        var result = RequireChild(XDocument.Parse(response).Root!, "invoiceDataResult");
        var payload = RequireText(result, "invoiceData");
        var compressed = ParseBool(RequireText(result, "compressedContentIndicator"), "compressedContentIndicator");
        return InvoiceDataDecoder.Read(payload, compressed);
    }

    public void Dispose()
    {
        _sender.Dispose();
    }

    private static DigestPage ParseDigest(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new NavQueryException("A kivonat válasz nem olvasható.", exception);
        }

        var result = RequireChild(document.Root!, "invoiceDigestResult");
        var currentPage = ParseInt(RequireText(result, "currentPage"), "currentPage");
        var availablePage = ParseInt(RequireText(result, "availablePage"), "availablePage");
        var items = result
            .Elements()
            .Where(element => element.Name.LocalName == "invoiceDigest")
            .Select(ParseItem)
            .ToArray();
        if (currentPage < 1)
        {
            throw new NavQueryException("A kivonat lapszáma hiányos.");
        }

        return new DigestPage(availablePage, items);
    }

    private static InvoiceDigestItem ParseItem(XElement element)
    {
        return new InvoiceDigestItem
        {
            InvoiceNumber = RequireText(element, "invoiceNumber"),
            BatchIndex = OptionalInt(element, "batchIndex"),
            InvoiceOperation = RequireText(element, "invoiceOperation"),
            InvoiceCategory = RequireText(element, "invoiceCategory"),
            InvoiceIssueDate = ParseDate(RequireText(element, "invoiceIssueDate"), "invoiceIssueDate"),
            SupplierTaxNumber = RequireText(element, "supplierTaxNumber"),
            SupplierGroupMemberTaxNumber = OptionalText(element, "supplierGroupMemberTaxNumber"),
            SupplierName = RequireText(element, "supplierName"),
            CustomerTaxNumber = OptionalText(element, "customerTaxNumber"),
            CustomerGroupMemberTaxNumber = OptionalText(element, "customerGroupMemberTaxNumber"),
            CustomerName = OptionalText(element, "customerName"),
            PaymentMethod = OptionalText(element, "paymentMethod"),
            PaymentDate = OptionalDate(element, "paymentDate"),
            InvoiceAppearance = OptionalText(element, "invoiceAppearance"),
            Source = OptionalText(element, "source"),
            InvoiceDeliveryDate = OptionalDate(element, "invoiceDeliveryDate"),
            Currency = OptionalText(element, "currency"),
            InvoiceNetAmount = OptionalDecimal(element, "invoiceNetAmount"),
            InvoiceNetAmountHuf = OptionalDecimal(element, "invoiceNetAmountHUF"),
            InvoiceVatAmount = OptionalDecimal(element, "invoiceVatAmount"),
            InvoiceVatAmountHuf = OptionalDecimal(element, "invoiceVatAmountHUF"),
            TransactionId = OptionalText(element, "transactionId"),
            Index = OptionalInt(element, "index"),
            OriginalInvoiceNumber = OptionalText(element, "originalInvoiceNumber"),
            ModificationIndex = OptionalInt(element, "modificationIndex"),
            InsDate = ParseTimestamp(RequireText(element, "insDate"), "insDate"),
            CompletenessIndicator = OptionalBool(element, "completenessIndicator")
        };
    }

    private static XElement RequireChild(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)
            ?? throw new NavQueryException($"A NAV-válaszból hiányzik a {localName}.");
    }

    private static string RequireText(XElement parent, string localName)
    {
        var text = OptionalText(parent, localName);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NavQueryException($"A NAV-válaszból hiányzik a {localName}.");
        }

        return text;
    }

    private static string? OptionalText(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value;
    }

    private static int ParseInt(string text, string name)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new NavQueryException($"A NAV-válasz {name} értéke nem olvasható.");
        }

        return value;
    }

    private static int? OptionalInt(XElement parent, string localName)
    {
        var text = OptionalText(parent, localName);
        return text is null ? null : ParseInt(text, localName);
    }

    private static decimal? OptionalDecimal(XElement parent, string localName)
    {
        var text = OptionalText(parent, localName);
        if (text is null)
        {
            return null;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            throw new NavQueryException($"A NAV-válasz {localName} értéke nem olvasható.");
        }

        return value;
    }

    private static DateOnly ParseDate(string text, string name)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new NavQueryException($"A NAV-válasz {name} értéke nem olvasható.");
        }

        return value;
    }

    private static DateOnly? OptionalDate(XElement parent, string localName)
    {
        var text = OptionalText(parent, localName);
        return text is null ? null : ParseDate(text, localName);
    }

    private static DateTimeOffset ParseTimestamp(string text, string name)
    {
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            throw new NavQueryException($"A NAV-válasz {name} értéke nem olvasható.");
        }

        return value;
    }

    private static bool ParseBool(string text, string name)
    {
        if (!bool.TryParse(text, out var value))
        {
            throw new NavQueryException($"A NAV-válasz {name} értéke nem olvasható.");
        }

        return value;
    }

    private static bool? OptionalBool(XElement parent, string localName)
    {
        var text = OptionalText(parent, localName);
        return text is null ? null : ParseBool(text, localName);
    }

    private sealed record DigestPage(int AvailablePage, IReadOnlyList<InvoiceDigestItem> Items);
}
