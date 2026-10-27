using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OsaPro.Excel;
using OsaPro.NAV;
using OsaPro.Worker;
using InvoiceDocument = OsaPro.NAV.InvoiceData.InvoiceData;

namespace OsaPro.Worker.Tests;

public class InvoiceProcessingTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly string[] ForbiddenFields =
    [
        "SendStatus",
        "RetryCount",
        "Retry",
        "Channel",
        "Timestamp",
        "TimeStamp",
        "Küldési állapot",
        "Újrapróbálás",
        "Csatorna"
    ];

    [Fact]
    public void Slices_share_the_boundary_and_keep_case_sensitive_identity()
    {
        var start = new DateOnly(2026, 1, 1);
        var stored = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
        var slices = ProcessingSlices.ForRun(start, stored, now);
        var boundary = new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(boundary, slices[0].To);
        Assert.Equal(boundary, slices[1].From);
        Assert.All(slices, slice => Assert.True(slice.To - slice.From <= TimeSpan.FromDays(ProcessingSlices.MaxDays)));

        var clamped = ProcessingSlices.ForRun(
            start,
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            now);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), clamped[0].From);
        Assert.NotEqual(InvoiceIdentity.Create("12345678", "Ab-1"), InvoiceIdentity.Create("12345678", "ab-1"));
    }

    [Fact]
    public void Row_uses_sent_amounts_card_label_and_empty_user_columns()
    {
        var negative = InvoiceRows.Build("Partner", "12345678-1-11", "SZ-1", null, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15), -1500m, -27m, "11111111-22222222-33333333", "CARD", "27%");
        var missing = InvoiceRows.Build("Partner", "12345678", "SZ-2", null, new DateOnly(2026, 3, 1), null, null, null, null, null, "");

        Assert.Equal("12345678", negative.TaxNumber);
        Assert.Equal(-1500m, negative.Gross);
        Assert.Equal("Bankkártya", negative.PaymentMethod);
        Assert.Equal("", negative.InvoicePresent);
        Assert.Equal("", negative.Paid);
        Assert.Equal("", negative.CarOrder);
        Assert.Equal("", negative.Note);
        Assert.Null(missing.Gross);
        Assert.Equal(new DateOnly(2026, 3, 1), missing.DueDate);
        Assert.Equal("", missing.BankAccount);
        Assert.Equal("", missing.PaymentMethod);
    }

    [Fact]
    public void Vat_text_keeps_first_seen_order_and_simplified_pairs()
    {
        var mixed = VatContentText.Join(
            [
                VatMarker.Percentage(0.27m),
                VatMarker.Percentage(0.27m),
                VatMarker.Exemption("AAM"),
                VatMarker.Exemption("TAM")
            ],
            simplified: false);
        var simplified = VatContentText.Join([VatMarker.Content(0.2126m)], simplified: true);
        var normal = VatContentText.Join(
            [VatMarker.Percentage(0.27m), VatMarker.Content(0.2126m)],
            simplified: false);
        var markers = VatContentText.Join(
            [
                VatMarker.ReverseCharge(),
                VatMarker.ReverseCharge(),
                VatMarker.Margin("TRAVEL_AGENCY"),
                VatMarker.NoCharge()
            ],
            simplified: false);

        Assert.Equal("27%,AAM,TAM", mixed);
        Assert.Equal("27%", simplified);
        Assert.Equal("27%", normal);
        Assert.Equal("FAD,TRAVEL_AGENCY,noVatCharge", markers);
        Assert.Equal("0%", VatContentText.Join([VatMarker.Percentage(0m)], simplified: false));
    }

    [Fact]
    public void Links_append_once_and_keep_a_missing_original()
    {
        var original = new TrackedInvoice { Year = 2026, TaxNumber = "12345678", InvoiceNumber = "EREDETI", Related = "SZ-1" };
        var later = InvoiceLinks.Decide(
            [original, new TrackedInvoice { Year = 2026, TaxNumber = "12345678", InvoiceNumber = "KESOBB", Related = "" }],
            [new SliceLinkItem { Operation = "MODIFY", TaxNumber = "12345678", InvoiceNumber = "SZ-2", OriginalInvoiceNumber = "KESOBB" }]);
        Assert.Empty(later.Missing);
        Assert.Equal("SZ-2", later.Updates.Single().Related);

        var missing = InvoiceLinks.Decide(
            [new TrackedInvoice { Year = 2026, TaxNumber = "12345678", InvoiceNumber = "SZ-2", Related = "" }],
            [new SliceLinkItem { Operation = "STORNO", TaxNumber = "12345678", InvoiceNumber = "SZ-2", OriginalInvoiceNumber = "NINCS" }]);
        Assert.Equal(InvoiceIdentity.Create("12345678", "SZ-2"), missing.Missing.Single());

        var saved = new TrackedInvoice { Year = 2026, TaxNumber = "12345678", InvoiceNumber = "EREDETI", Related = "SZ-1" };
        var retry = InvoiceLinks.Decide(
            [
                saved,
                new TrackedInvoice { Year = 2026, TaxNumber = "12345678", InvoiceNumber = "SZ-2", Related = "" }
            ],
            [new SliceLinkItem { Operation = "MODIFY", TaxNumber = "12345678", InvoiceNumber = "SZ-2", OriginalInvoiceNumber = "EREDETI" }]);
        Assert.Equal("SZ-1;SZ-2", retry.Updates.Single().Related);
        Assert.Empty(retry.Missing);
        Assert.Equal("SZ-1;SZ-2", InvoiceLinks.Append("SZ-1;SZ-2", "SZ-2"));
    }

    [Fact]
    public void Error_add_is_idempotent_and_table_fields_have_no_delivery_columns()
    {
        var log = new MemoryNotificationLog();
        var note = NotificationRecords.OutOfYearNote(
            InvoiceIdentity.Create("12345678", "SZ-1"),
            "Partner",
            new DateOnly(2024, 1, 2),
            new DateOnly(2024, 1, 16),
            -1500m);
        log.Add(note);
        log.Add(note);

        Assert.Single(log.All);
        var fields = NotificationRecords.TableFields(note);
        Assert.Equal(NotificationNote.ErrorType, fields["Type"]);
        Assert.Equal(TableNotificationLog.TableName, "notification");
        Assert.Contains("A kelte a kezelt éveken kívül van.", fields["Message"], StringComparison.Ordinal);
        Assert.Contains("-1500", fields["Message"], StringComparison.Ordinal);
        Assert.Equal("12345678", fields["Taxnumber"]);
        Assert.Equal("Partner", fields["Partnername"]);
        Assert.Equal("SZ-1", fields["Invoice"]);
        Assert.DoesNotContain(fields.Keys, key => ForbiddenFields.Contains(key, StringComparer.Ordinal));
        var outside = NotificationRecords.OutOfYearNote(InvoiceIdentity.Create("12345678", "REGI"), "Partner", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 1), null);
        Assert.DoesNotContain("missing-original", outside.Identity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cycle_uploads_after_nav_and_stops_when_a_later_slice_fails()
    {
        var registry = Registry();
        var digest = new FakeDigest();
        var invoices = new FakeInvoices();
        digest.Pages.Add(digestItem => digestItem.From == new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero)
            ? throw new InvalidOperationException("nav")
            : [Item("CREATE", "SZ-1", new DateOnly(2026, 1, 20))]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(registry, digest, invoices, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero)));

        var calls = registry.Trace;
        Assert.Equal(["excel", "nav", "excel", "upload", "nav"], calls);
        Assert.True(calls.IndexOf("upload") < calls.LastIndexOf("nav"));
        Assert.Single(registry.Errors.All);
        Assert.Equal(NotificationNote.ErrorType, registry.Errors.All.Single().Type);
        Assert.Contains("A ciklus hibával leállt", registry.Errors.All.Single().Message, StringComparison.Ordinal);
        Assert.Equal("", registry.Errors.All.Single().Taxnumber);
        Assert.Equal("", registry.Errors.All.Single().Partnername);
        Assert.Equal("", registry.Errors.All.Single().Invoice);
        Assert.DoesNotContain(calls, call => call.Contains("checkout", StringComparison.Ordinal));
        registry.Errors.Add(registry.Errors.All.Single());
        Assert.Single(registry.Errors.All);
    }

    [Fact]
    public async Task Empty_slice_advances_ins_date_and_failed_current_upload_keeps_the_row()
    {
        var registry = Registry();
        var digest = new FakeDigest();
        var invoices = new FakeInvoices();
        digest.Pages.Add(_ => [Item("CREATE", "SZ-ELOZO", new DateOnly(2025, 6, 2))]);
        registry.FailCurrentYear = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(registry, digest, invoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal([2025, 2026], registry.UploadOrder);
        Assert.Contains(registry.Files[2025].Length, new[] { registry.Files[2025].Length });
        Assert.Contains("SZ-ELOZO", Invoices(registry.Files[2025]));
        Assert.False(registry.Files.ContainsKey(2026));
        var again = new FakeDigest();
        again.Pages.Add(_ => [Item("CREATE", "SZ-ELOZO", new DateOnly(2025, 6, 2))]);
        var secondInvoices = new FakeInvoices();
        registry.FailCurrentYear = false;
        await Run(registry, again, secondInvoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, Invoices(registry.Files[2025]).Count(number => number == "SZ-ELOZO"));
        Assert.Equal(0, secondInvoices.Calls);
        Assert.Equal("2026-01-21T00:00:00.000Z", Parameter(registry.Files[2026], "B1"));
        var empty = new FakeDigest();
        empty.Pages.Add(_ => []);
        await Run(registry, empty, new FakeInvoices(), new DateTimeOffset(2026, 1, 22, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal("2026-01-23T00:00:00.000Z", Parameter(registry.Files[2026], "B1"));
    }

    [Fact]
    public async Task Invalid_configuration_and_missing_ins_date_stop_before_nav()
    {
        var registry = Registry();
        var digest = new FakeDigest();
        var invoices = new FakeInvoices();
        await Assert.ThrowsAsync<ProcessingException>(() => Run(registry, digest, invoices, Now, startDate: ""));
        Assert.Throws<ProcessingException>(() => TableNotificationLog.Open(""));
        registry.Files[2025] = ParametersOnly();
        await Assert.ThrowsAsync<MissingInsDateException>(() => Run(registry, digest, invoices, Now));
        Assert.Empty(digest.Calls);
        Assert.Empty(registry.UploadOrder);
    }

    [Fact]
    public async Task First_pass_skips_registered_and_out_of_year_and_annul()
    {
        var registry = Registry(withInvoice: true);
        var digest = new FakeDigest();
        var invoices = new FakeInvoices();
        digest.Pages.Add(_ =>
        [
            Item("CREATE", "SZ-BENT", new DateOnly(2026, 1, 10)),
            Item("CREATE", "SZ-UJ", new DateOnly(2026, 1, 11)),
            Item("CREATE", "SZ-REGI", new DateOnly(2024, 1, 11)),
            Item("ANNUL", "SZ-ANNUL", new DateOnly(2026, 1, 12))
        ]);

        await Run(registry, digest, invoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, invoices.Calls);
        Assert.Equal(2, invoices.BatchIndexes.Count(index => index is null));
        Assert.DoesNotContain("SZ-REGI", Invoices(registry.Files[2026]).Concat(Invoices(registry.Files.GetValueOrDefault(2025) ?? [])));
        Assert.DoesNotContain("SZ-ANNUL", Invoices(registry.Files[2026]));
        Assert.Contains("SZ-UJ", Invoices(registry.Files[2026]));
        var outside = Assert.Single(registry.Errors.All, note => note.Message.Contains("kezelt éveken kívül", StringComparison.Ordinal));
        Assert.Contains("SZ-REGI", outside.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Links_same_slice_original_and_keeps_a_missing_original_record()
    {
        var registry = Registry();
        var digest = new FakeDigest();
        var invoices = new FakeInvoices();
        digest.Pages.Add(_ =>
        [
            Item("CREATE", "EREDETI", new DateOnly(2026, 1, 10)),
            Item("MODIFY", "SZ-2", new DateOnly(2026, 1, 11), original: "EREDETI", batch: 4)
        ]);
        await Run(registry, digest, invoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(4, invoices.BatchIndexes.Last());
        Assert.Equal("SZ-2", Related(registry.Files[2026], "EREDETI"));
        Assert.DoesNotContain(registry.Errors.All, note => note.Message.Contains("Az eredeti nincs", StringComparison.Ordinal));

        var missingRegistry = Registry();
        var missingDigest = new FakeDigest();
        var missingInvoices = new FakeInvoices();
        missingDigest.Pages.Add(_ => [Item("STORNO", "SZ-3", new DateOnly(2026, 1, 12), original: "NINCS")]);
        await Run(missingRegistry, missingDigest, missingInvoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal("NINCS", Original(missingRegistry.Files[2026], "SZ-3"));
        var record = Assert.Single(missingRegistry.Errors.All);
        Assert.Contains("Az eredeti nincs a nyilvántartásban.", record.Message, StringComparison.Ordinal);
        Assert.Contains("NINCS", record.Message, StringComparison.Ordinal);
        missingDigest.Pages.Clear();
        missingDigest.Pages.Add(_ => [Item("STORNO", "SZ-3", new DateOnly(2026, 1, 12), original: "NINCS")]);
        await Run(missingRegistry, missingDigest, missingInvoices, new DateTimeOffset(2026, 1, 21, 12, 0, 0, TimeSpan.Zero));
        Assert.Single(missingRegistry.Errors.All);
    }

    [Fact]
    public async Task Repeated_slice_appends_a_missing_link_and_the_next_run_starts_at_midnight()
    {
        var registry = Registry(withInvoice: true, related: "");
        var saved = Enumerable.Repeat("", 16).ToArray();
        saved[1] = "12345678";
        saved[3] = "SZ-2";
        saved[4] = "SZ-BENT";
        saved[15] = "megjegyzés";
        registry.Files[2026] = WorkbookFile.Apply(
            registry.Files[2026],
            new RegistryEdit
            {
                Year = 2026,
                CurrentYear = 2026,
                SheetMonth = 1,
                NewRows = [new RegistryRowWrite { Month = 1, Cells = saved }]
            });
        var digest = new FakeDigest();
        var invoices = new FakeInvoices();
        digest.Pages.Add(_ => [Item("MODIFY", "SZ-2", new DateOnly(2026, 1, 11), original: "SZ-BENT")]);
        await Run(registry, digest, invoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(0, invoices.Calls);
        Assert.Equal("SZ-2", Related(registry.Files[2026], "SZ-BENT"));
        Assert.Equal("megjegyzés", Note(registry.Files[2026], "SZ-BENT"));
        Assert.Empty(registry.Errors.All);

        var from = digest.Ranges.Last().From;
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), from);
        var next = new FakeDigest();
        var nextInvoices = new FakeInvoices();
        next.Pages.Add(_ => [Item("CREATE", "SZ-BENT", new DateOnly(2026, 1, 10))]);
        await Run(registry, next, nextInvoices, new DateTimeOffset(2026, 1, 25, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 1, 21, 0, 0, 0, TimeSpan.Zero), next.Ranges[0].From);
        Assert.Equal(0, nextInvoices.Calls);
        Assert.Equal(1, Invoices(registry.Files[2026]).Count(number => number == "SZ-BENT"));
        Assert.DoesNotContain("Slack", File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "InvoiceProcessing.cs"))), StringComparison.Ordinal);
        Assert.DoesNotContain("TimerTrigger", File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "FunctionHandlers.cs"))), StringComparison.Ordinal);
        Assert.DoesNotContain("HttpTrigger", File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "FunctionHandlers.cs"))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Full_invoice_failure_does_not_advance_ins_date()
    {
        var registry = Registry();
        var before = registry.Files[2025].ToArray();
        var digest = new FakeDigest();
        var invoices = new FakeInvoices { Fail = true };
        digest.Pages.Add(_ => [Item("CREATE", "SZ-1", new DateOnly(2026, 1, 10))]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(registry, digest, invoices, new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero)));

        Assert.Empty(registry.UploadOrder);
        Assert.Equal(before, registry.Files[2025]);
        Assert.Single(registry.Errors.All, note => note.Type == NotificationNote.ErrorType && note.Message.Contains("A ciklus hibával leállt", StringComparison.Ordinal) && note.Taxnumber == "" && note.Partnername == "" && note.Invoice == "");
        registry.Errors.Add(registry.Errors.All.Single());
        Assert.Single(registry.Errors.All);
    }

    private static async Task Run(
        FakeRegistry registry,
        FakeDigest digest,
        FakeInvoices invoices,
        DateTimeOffset now,
        string? startDate = "2026-01-01")
    {
        digest.Log = registry.Trace;
        await InvoiceProcessing.Run(
            new ProcessingRun
            {
                StartDate = startDate,
                Now = now,
                Digest = digest,
                Invoices = invoices,
                Registry = registry
            },
            registry.Errors);
    }

    private static FakeRegistry Registry(bool withInvoice = false, string related = "")
    {
        var cells = Enumerable.Repeat("", 16).ToArray();
        cells[0] = "Bent";
        cells[1] = "12345678";
        cells[3] = "SZ-BENT";
        cells[5] = related;
        cells[15] = "megjegyzés";
        var previous = WorkbookFile.Create(2025, 6, "2026-01-01T08:00:00Z", "7,5,1");
        var registry = new FakeRegistry();
        registry.Files[2025] = previous;
        if (withInvoice)
        {
            registry.Files[2026] = WorkbookFile.Apply(
                WorkbookFile.Create(2026, 1, "2026-01-01T08:00:00Z", "7,5,1"),
                new RegistryEdit
                {
                    Year = 2026,
                    CurrentYear = 2026,
                    SheetMonth = 1,
                    NewRows = [new RegistryRowWrite { Month = 1, Cells = cells }]
                });
        }

        return registry;
    }

    private static byte[] ParametersOnly()
    {
        return WorkbookFile.Create(2025, 6, "", "7,5,1");
    }

    private static string Parameter(byte[] bytes, string reference)
    {
        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var sheet = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == "Paraméterek");
        var part = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
        return part.Worksheet.Descendants<Cell>().Single(cell => cell.CellReference == reference).InlineString!.InnerText;
    }

    private static IReadOnlyList<string> Invoices(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return [];
        }

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        return document.WorkbookPart!.WorksheetParts
            .SelectMany(part => part.Worksheet.Descendants<Cell>())
            .Where(cell => cell.CellReference?.Value is string reference && reference.StartsWith("D", StringComparison.Ordinal) && reference != "D1")
            .Select(cell => cell.InlineString?.InnerText ?? "")
            .Where(text => text.Length > 0)
            .ToArray();
    }

    private static string Related(byte[] bytes, string invoice)
    {
        return Cell(bytes, invoice, "F");
    }

    private static string Original(byte[] bytes, string invoice)
    {
        return Cell(bytes, invoice, "E");
    }

    private static string Note(byte[] bytes, string invoice)
    {
        return Cell(bytes, invoice, "P");
    }

    private static string Cell(byte[] bytes, string invoice, string column)
    {
        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        foreach (var part in document.WorkbookPart!.WorksheetParts)
        {
            var rows = part.Worksheet.Descendants<Row>();
            foreach (var row in rows)
            {
                var number = row.Elements<Cell>().FirstOrDefault(cell => cell.CellReference?.Value == "D" + row.RowIndex)?.InlineString?.InnerText;
                if (number != invoice)
                {
                    continue;
                }

                return row.Elements<Cell>().FirstOrDefault(cell => cell.CellReference?.Value == column + row.RowIndex)?.InlineString?.InnerText ?? "";
            }
        }

        return "";
    }

    private static InvoiceDigestItem Item(
        string operation,
        string number,
        DateOnly date,
        string? original = null,
        int? batch = null)
    {
        return new InvoiceDigestItem
        {
            InvoiceNumber = number,
            BatchIndex = batch,
            InvoiceOperation = operation,
            InvoiceCategory = "NORMAL",
            InvoiceIssueDate = date,
            SupplierTaxNumber = "12345678",
            SupplierName = "Partner",
            PaymentMethod = "TRANSFER",
            PaymentDate = date.AddDays(14),
            InvoiceVatAmount = 27m,
            OriginalInvoiceNumber = original,
            InsDate = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class FakeDigest : IDigestSource
    {
        public List<Func<(DateTimeOffset From, DateTimeOffset To), InvoiceDigestItem[]>> Pages { get; } = [];

        public List<string> Log { get; set; } = [];

        public List<(DateTimeOffset From, DateTimeOffset To)> Ranges { get; } = [];

        public IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> Calls => Ranges;

        public Task<IReadOnlyList<InvoiceDigestItem>> QueryRange(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        {
            Log.Add("nav");
            Ranges.Add((from, to));
            var page = Pages[Math.Min(Pages.Count - 1, Ranges.Count - 1)];
            return Task.FromResult<IReadOnlyList<InvoiceDigestItem>>(page((from, to)));
        }
    }

    private sealed class FakeInvoices : IInvoiceSource
    {
        public int Calls { get; private set; }

        public List<int?> BatchIndexes { get; } = [];

        public bool Fail { get; init; }

        public Task<InvoiceDocument> Query(string invoiceNumber, string supplierTaxNumber, int? batchIndex, CancellationToken cancellationToken)
        {
            Calls++;
            BatchIndexes.Add(batchIndex);
            if (Fail)
            {
                throw new InvalidOperationException("teljes bizonylat");
            }

            return Task.FromResult(new InvoiceDocument());
        }
    }

    private sealed class FakeRegistry : IRegistryPort
    {
        public Dictionary<int, byte[]> Files { get; } = [];

        public MemoryNotificationLog Errors { get; } = new();

        public List<string> Trace { get; } = [];

        public List<int> UploadOrder { get; } = [];

        public bool FailCurrentYear { get; set; }

        public Task<RegistrySnapshot> Download(CancellationToken cancellationToken)
        {
            Trace.Add("excel");
            return Task.FromResult(WorkbookFile.ReadSnapshot(new Dictionary<int, byte[]>(Files), Now));
        }

        public Task Upload(IReadOnlyList<(int Year, byte[] Bytes)> files, int currentYear, CancellationToken cancellationToken)
        {
            Trace.Add("upload");
            foreach (var (year, bytes) in files)
            {
                UploadOrder.Add(year);
                if (FailCurrentYear && year == currentYear)
                {
                    throw new InvalidOperationException("feltöltés");
                }

                Files[year] = bytes;
            }

            return Task.CompletedTask;
        }
    }
}
