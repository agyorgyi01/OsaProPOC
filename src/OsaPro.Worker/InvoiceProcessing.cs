using System.Globalization;
using Microsoft.Extensions.Configuration;
using OsaPro.Excel;
using OsaPro.NAV;
using OsaPro.NAV.InvoiceBase;
using InvoiceDocument = OsaPro.NAV.InvoiceData.InvoiceData;
using InvoiceType = OsaPro.NAV.InvoiceData.InvoiceType;
using MarginSchemeType = OsaPro.NAV.InvoiceData.MarginSchemeType;
using VatRateType = OsaPro.NAV.InvoiceData.VatRateType;

namespace OsaPro.Worker;

internal interface IDigestSource
{
    Task<IReadOnlyList<InvoiceDigestItem>> QueryRange(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

internal interface IInvoiceSource
{
    Task<InvoiceDocument> Query(
        string invoiceNumber,
        string supplierTaxNumber,
        int? batchIndex,
        CancellationToken cancellationToken);
}

internal interface IRegistryPort
{
    Task<RegistrySnapshot> Download(CancellationToken cancellationToken);

    Task Upload(IReadOnlyList<(int Year, byte[] Bytes)> files, int currentYear, CancellationToken cancellationToken);
}

internal sealed class ProcessingRun
{
    public required string? StartDate { get; init; }

    public required DateTimeOffset Now { get; init; }

    public required IDigestSource Digest { get; init; }

    public required IInvoiceSource Invoices { get; init; }

    public required IRegistryPort Registry { get; init; }
}

internal static class InvoiceProcessing
{
    public static async Task RunConfigured(
        IConfiguration configuration,
        INotificationLog notifications,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        using var navHandler = new HttpClientHandler();
        using var excelHandler = new HttpClientHandler();
        using var query = new InboundInvoiceQuery(
            NavOptions.FromConfiguration(configuration),
            navHandler,
            () => now);
        using var workbook = new ExcelWorkbook(
            ExcelOptions.FromConfiguration(configuration),
            excelHandler,
            () => now);
        await Run(
            new ProcessingRun
            {
                StartDate = configuration["Processing:StartDate"],
                Now = now,
                Digest = new NavDigestSource(query),
                Invoices = new NavInvoiceSource(query),
                Registry = new ExcelRegistryPort(workbook)
            },
            notifications,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task Run(
        ProcessingRun run,
        INotificationLog notifications,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParseExact(
                run.StartDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startDate))
        {
            throw new ProcessingException("A kezdődátum hiányzik vagy nem yyyy-MM-dd alakú.");
        }

        RegistrySnapshot cursor;
        try
        {
            cursor = await run.Registry.Download(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not MissingInsDateException)
        {
            notifications.Add(NotificationRecords.CursorNote());
            throw;
        }

        var currentYear = WorkbookRules.CurrentYear(run.Now);
        var insDate = cursor.HasFile
            ? ParseTimestamp(cursor.InsDateText)
            : ParseTimestamp(WorkbookRules.DefaultInsDate(currentYear));
        var slices = ProcessingSlices.ForRun(startDate, insDate, run.Now);
        foreach (var slice in slices)
        {
            var stage = "nav";
            try
            {
                var items = await run.Digest.QueryRange(slice.From, slice.To, cancellationToken).ConfigureAwait(false);
                stage = "excel";
                var downloaded = await run.Registry.Download(cancellationToken).ConfigureAwait(false);
                stage = "apply";
                var edited = await ApplySlice(run, notifications, downloaded, items, slice.To, cancellationToken).ConfigureAwait(false);
                stage = "upload";
                await run.Registry.Upload(edited, currentYear, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                notifications.Add(NotificationRecords.CycleNote(slice.From, slice.To, stage));
                throw;
            }
        }
    }

    private static async Task<IReadOnlyList<(int Year, byte[] Bytes)>> ApplySlice(
        ProcessingRun run,
        INotificationLog notifications,
        RegistrySnapshot snapshot,
        IReadOnlyList<InvoiceDigestItem> items,
        DateTimeOffset sliceEnd,
        CancellationToken cancellationToken)
    {
        var tracked = snapshot.Rows.Select(row => new TrackedInvoice
        {
            Year = row.Year,
            TaxNumber = Text(row, 1),
            InvoiceNumber = Text(row, 3),
            Related = Text(row, 5)
        }).ToList();
        var known = new HashSet<InvoiceIdentity>(tracked.Select(row => InvoiceIdentity.Create(row.TaxNumber, row.InvoiceNumber)));
        var linkItems = new List<SliceLinkItem>();
        var drafts = new Dictionary<InvoiceIdentity, InvoiceRowDraft>();
        var rowsByYear = new Dictionary<int, List<RegistryRowWrite>>();

        foreach (var item in items)
        {
            if (item.InvoiceOperation is not ("CREATE" or "MODIFY" or "STORNO"))
            {
                continue;
            }

            var identity = InvoiceIdentity.Create(InvoiceRows.TaxNumber(item.SupplierTaxNumber), item.InvoiceNumber);
            var handled = Handled(item.InvoiceIssueDate, run.Now);
            if (known.Contains(identity))
            {
                if (handled)
                {
                    AddLink(linkItems, item);
                }

                continue;
            }

            var document = await run.Invoices.Query(
                item.InvoiceNumber,
                item.SupplierTaxNumber,
                item.InvoiceOperation == "MODIFY" ? item.BatchIndex : null,
                cancellationToken).ConfigureAwait(false);
            var full = ReadFull(document, item.BatchIndex);
            var due = item.PaymentDate ?? item.InvoiceIssueDate;
            if (!handled)
            {
                notifications.Add(NotificationRecords.OutOfYearNote(
                    identity,
                    item.SupplierName,
                    item.InvoiceIssueDate,
                    due,
                    full.Gross));
                continue;
            }

            var draft = InvoiceRows.Build(
                item.SupplierName,
                item.SupplierTaxNumber,
                item.InvoiceNumber,
                item.InvoiceOperation is "CREATE" ? null : item.OriginalInvoiceNumber,
                item.InvoiceIssueDate,
                item.PaymentDate,
                full.Gross,
                item.InvoiceVatAmount,
                full.BankAccount,
                full.PaymentCode ?? item.PaymentMethod,
                full.VatContent);
            drafts[identity] = draft;
            known.Add(identity);
            var year = item.InvoiceIssueDate.Year;
            tracked.Add(new TrackedInvoice
            {
                Year = year,
                TaxNumber = draft.TaxNumber,
                InvoiceNumber = draft.InvoiceNumber,
                Related = ""
            });
            if (!rowsByYear.TryGetValue(year, out var bucket))
            {
                bucket = [];
                rowsByYear[year] = bucket;
            }

            bucket.Add(new RegistryRowWrite { Month = item.InvoiceIssueDate.Month, Cells = Cells(draft) });
            if (item.InvoiceOperation is "MODIFY" or "STORNO")
            {
                notifications.Add(NotificationRecords.DocumentNote(
                    draft.TaxNumber,
                    draft.Partner,
                    draft.InvoiceNumber,
                    item.OriginalInvoiceNumber,
                    item.InvoiceOperation));
            }

            AddLink(linkItems, item);
        }

        var (updates, missing) = InvoiceLinks.Decide(tracked, linkItems);
        foreach (var identity in missing)
        {
            var item = items.First(candidate =>
                InvoiceIdentity.Create(InvoiceRows.TaxNumber(candidate.SupplierTaxNumber), candidate.InvoiceNumber) == identity);
            drafts.TryGetValue(identity, out var draft);
            notifications.Add(NotificationRecords.MissingOriginalNote(
                identity,
                draft?.Partner ?? item.SupplierName,
                item.InvoiceIssueDate,
                draft?.DueDate ?? item.PaymentDate ?? item.InvoiceIssueDate,
                draft?.Gross,
                item.OriginalInvoiceNumber ?? ""));
        }

        var linksByYear = new Dictionary<int, List<RegistryLinkWrite>>();
        foreach (var update in updates)
        {
            var owner = tracked.First(row =>
                InvoiceIdentity.Create(row.TaxNumber, row.InvoiceNumber)
                == InvoiceIdentity.Create(update.TaxNumber, update.InvoiceNumber));
            if (!linksByYear.TryGetValue(owner.Year, out var bucket))
            {
                bucket = [];
                linksByYear[owner.Year] = bucket;
            }

            bucket.Add(new RegistryLinkWrite
            {
                TaxNumber = owner.TaxNumber,
                InvoiceNumber = owner.InvoiceNumber,
                Related = update.Related
            });
        }

        var currentYear = WorkbookRules.CurrentYear(run.Now);
        var years = rowsByYear.Keys.Concat(linksByYear.Keys).Append(currentYear).Distinct().ToArray();
        var edited = new List<(int Year, byte[] Bytes)>();
        foreach (var year in years.OrderBy(year => year == currentYear).ThenBy(year => year))
        {
            var carried = snapshot.HasFile ? snapshot.InsDateText : WorkbookRules.DefaultInsDate(currentYear);
            var edit = new RegistryEdit
            {
                Year = year,
                CurrentYear = currentYear,
                SheetMonth = WorkbookRules.CurrentMonth(run.Now),
                CarriedInsDate = carried,
                CarriedNoticeDays = string.IsNullOrWhiteSpace(snapshot.NoticeDays)
                    ? WorkbookRules.DefaultNoticeDays
                    : snapshot.NoticeDays,
                WriteInsDate = year == currentYear,
                InsDate = Timestamp(sliceEnd),
                NewRows = rowsByYear.GetValueOrDefault(year) ?? [],
                Links = linksByYear.GetValueOrDefault(year) ?? []
            };
            var source = snapshot.Files.TryGetValue(year, out var bytes) ? bytes : null;
            edited.Add((year, WorkbookFile.Apply(source, edit)));
        }

        return edited;
    }

    private static void AddLink(List<SliceLinkItem> linkItems, InvoiceDigestItem item)
    {
        if (item.InvoiceOperation is not ("MODIFY" or "STORNO"))
        {
            return;
        }

        linkItems.Add(new SliceLinkItem
        {
            Operation = item.InvoiceOperation,
            TaxNumber = InvoiceRows.TaxNumber(item.SupplierTaxNumber),
            InvoiceNumber = item.InvoiceNumber.Trim(),
            OriginalInvoiceNumber = item.OriginalInvoiceNumber
        });
    }

    private static bool Handled(DateOnly issueDate, DateTimeOffset now)
    {
        var year = WorkbookRules.CurrentYear(now);
        return issueDate.Year == year || issueDate.Year == year - 1;
    }

    private static string Text(StoredInvoiceRow row, int index)
    {
        return index < row.Cells.Length ? row.Cells[index].Trim() : "";
    }

    private static string[] Cells(InvoiceRowDraft draft)
    {
        return
        [
            draft.Partner,
            draft.TaxNumber,
            draft.BankAccount,
            draft.InvoiceNumber,
            draft.OriginalInvoice,
            draft.Related,
            draft.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            draft.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            draft.Gross?.ToString(CultureInfo.InvariantCulture) ?? "",
            draft.VatContent,
            draft.VatAmount?.ToString(CultureInfo.InvariantCulture) ?? "",
            draft.PaymentMethod,
            draft.InvoicePresent,
            draft.Paid,
            draft.CarOrder,
            draft.Note
        ];
    }

    private static FullInvoice ReadFull(InvoiceDocument document, int? batchIndex)
    {
        var invoice = Select(document, batchIndex);
        var detail = invoice?.InvoiceHead?.InvoiceDetail;
        string? payment = detail is { PaymentMethodSpecified: true } ? PaymentCode(detail.PaymentMethod) : null;
        decimal? gross = invoice?.InvoiceSummary?.SummaryGrossData is { } summary ? summary.InvoiceGrossAmount : null;
        var bank = invoice?.InvoiceHead?.SupplierInfo?.SupplierBankAccountNumber;
        var simplified = detail is not null && detail.InvoiceCategory == InvoiceCategoryType.Simplified;
        var markers = new List<VatMarker>();
        if (invoice?.InvoiceLines?.Line is { } lines)
        {
            foreach (var line in lines)
            {
                var rate = line.LineAmountsNormal?.LineVatRate ?? line.LineAmountsSimplified?.LineVatRate;
                if (rate is null)
                {
                    continue;
                }

                if (Marker(rate) is VatMarker marker)
                {
                    markers.Add(marker);
                }
            }
        }

        return new FullInvoice(bank, gross, payment, VatContentText.Join(markers, simplified));
    }

    private static InvoiceType? Select(InvoiceDocument document, int? batchIndex)
    {
        var main = document.InvoiceMain;
        if (main is null)
        {
            return null;
        }

        if (batchIndex is int index)
        {
            foreach (var batch in main.BatchInvoice)
            {
                if (batch.BatchIndex == index)
                {
                    return batch.Invoice;
                }
            }
        }

        return main.Invoice;
    }

    private static VatMarker? Marker(VatRateType rate)
    {
        if (rate.VatExemption is not null)
        {
            return VatMarker.Exemption(rate.VatExemption.Case);
        }

        if (rate.VatOutOfScope is not null)
        {
            return VatMarker.OutOfScope(rate.VatOutOfScope.Case);
        }

        if (rate.VatAmountMismatch is not null)
        {
            return VatMarker.Mismatch(rate.VatAmountMismatch.Case);
        }

        if (rate.VatDomesticReverseChargeSpecified && rate.VatDomesticReverseCharge)
        {
            return VatMarker.ReverseCharge();
        }

        if (rate.MarginSchemeIndicatorSpecified)
        {
            return VatMarker.Margin(rate.MarginSchemeIndicator switch
            {
                MarginSchemeType.TravelAgency => "TRAVEL_AGENCY",
                MarginSchemeType.SecondHand => "SECOND_HAND",
                MarginSchemeType.Artwork => "ARTWORK",
                MarginSchemeType.Antiques => "ANTIQUES",
                _ => rate.MarginSchemeIndicator.ToString()
            });
        }

        if (rate.NoVatChargeSpecified && rate.NoVatCharge)
        {
            return VatMarker.NoCharge();
        }

        if (rate.VatPercentageSpecified)
        {
            return VatMarker.Percentage(rate.VatPercentage);
        }

        if (rate.VatContentSpecified)
        {
            return VatMarker.Content(rate.VatContent);
        }

        return null;
    }

    private static string PaymentCode(PaymentMethodType method)
    {
        return method switch
        {
            PaymentMethodType.Transfer => "TRANSFER",
            PaymentMethodType.Cash => "CASH",
            PaymentMethodType.Card => "CARD",
            PaymentMethodType.Voucher => "VOUCHER",
            PaymentMethodType.Other => "OTHER",
            _ => method.ToString()
        };
    }

    private static DateTimeOffset ParseTimestamp(string text)
    {
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            throw new MissingInsDateException();
        }

        return value;
    }

    private static string Timestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    private sealed record FullInvoice(string? BankAccount, decimal? Gross, string? PaymentCode, string VatContent);
}

internal sealed class NavDigestSource(InboundInvoiceQuery query) : IDigestSource
{
    public Task<IReadOnlyList<InvoiceDigestItem>> QueryRange(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        return query.QueryInvoiceDigestRange(from, to, cancellationToken);
    }
}

internal sealed class NavInvoiceSource(InboundInvoiceQuery query) : IInvoiceSource
{
    public Task<InvoiceDocument> Query(
        string invoiceNumber,
        string supplierTaxNumber,
        int? batchIndex,
        CancellationToken cancellationToken)
    {
        return query.QueryInvoiceData(invoiceNumber, supplierTaxNumber, batchIndex, cancellationToken);
    }
}

internal sealed class ExcelRegistryPort(ExcelWorkbook workbook) : IRegistryPort
{
    public Task<RegistrySnapshot> Download(CancellationToken cancellationToken)
    {
        return workbook.ReadRegistry(cancellationToken);
    }

    public Task Upload(IReadOnlyList<(int Year, byte[] Bytes)> files, int currentYear, CancellationToken cancellationToken)
    {
        return workbook.UploadFiles(files.ToDictionary(file => file.Year, file => file.Bytes), currentYear, cancellationToken);
    }
}
