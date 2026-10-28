using OsaPro.Excel;
using OsaPro.Worker;

namespace OsaPro.Worker.Tests;

public class NotificationCompositionTests
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Keys_keep_case()
    {
        var today = new DateOnly(2026, 10, 26);
        var log = new MemoryNotificationLog();
        var document = NotificationRecords.DocumentNote(" Ab-1 ", " Partner ", " Sz-1 ", " Eredeti ", "MODIFY");
        var payment = NotificationRecords.PaymentNote(today, "Ab-1", "Partner", "Sz-1", today, "10,00");
        var paid = NotificationRecords.PaidNote("Ab-1", "Partner", "Sz-1", "ROSSZ");
        var notice = NotificationRecords.NoticeDaysNote("7,0,1");
        var missing = NotificationRecords.MissingFileNote(2026, currentYear: true);
        var start = NotificationRecords.StartDateNote();
        var read = NotificationRecords.ReadErrorNote(today, 2026);

        Assert.Equal("Ab-1|Sz-1", document.Identity);
        Assert.Equal("Document", document.Type);
        Assert.Equal("2026-10-26|Ab-1|Sz-1", payment.Identity);
        Assert.Equal("Payment", payment.Type);
        Assert.Equal("paid|Ab-1|Sz-1", paid.Identity);
        Assert.Equal("notice-days", notice.Identity);
        Assert.Equal("missing-file|SZÁMLÁK2026.xlsx", missing.Identity);
        Assert.Equal("start-date", start.Identity);
        Assert.Equal("read|2026-10-26", read.Identity);

        foreach (var note in new[] { document, payment, paid, notice, missing, start, read })
        {
            Assert.False(NotificationRecords.TableFields(note).ContainsKey("NotificationTimestamp"));
            log.Add(note);
            log.Add(note);
        }

        Assert.Equal(7, log.All.Count);
    }

    [Fact]
    public void Payment_text_uses_the_serial_date_and_keeps_letter_case()
    {
        var serial = new DateOnly(2026, 10, 30).DayNumber - new DateOnly(1899, 12, 30).DayNumber;
        var notes = NoticeRules.Compose(
            new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero),
            [
                Invoice("Partner", " 111 ", "SZ-1", serial.ToString(), "1500.5", " Átutalás ", present: "bármi"),
                Invoice("Másik", "111", "SZ-1", serial.ToString(), "9", "Átutalás"),
                Invoice("Partner", "Ab-1", "SZ-2", "2026-10-30", "-1500", "Átutalás"),
                Invoice("Partner", "ab-1", "SZ-2", "2026-10-30", "", "Átutalás"),
                Invoice("Partner", "222", "SZ-3", "2026-10-30", "nem-szam", "Átutalás", paid: "igen"),
                Invoice("Partner", "666", "SZ-7", "2026-10-30", "10", "Átutalás", paid: " fizetve "),
                Invoice("Partner", "333", "SZ-4", "nem-datum", "10", "Átutalás", issue: ""),
                Invoice("Partner", "444", "SZ-5", "2026-10-30", "10", "Készpénz"),
                Invoice("Partner", "555", "SZ-6", "2026-10-30", "10", "átutalás")
            ],
            "7,5,1");

        var first = Assert.Single(notes, note => note.Invoice == "SZ-1");
        Assert.Equal("SZ-1 fizetés esedékes 2026.10.30, összeg: 1500,50", first.Message);
        Assert.Equal("111", first.Taxnumber);
        Assert.Equal("Partner", first.Partnername);
        Assert.Equal("Payment", first.Type);
        var negative = Assert.Single(notes, note => note.Taxnumber == "Ab-1");
        Assert.Equal("SZ-2 fizetés esedékes 2026.10.30, összeg: -1500,00", negative.Message);
        var empty = Assert.Single(notes, note => note.Taxnumber == "ab-1");
        Assert.Equal("SZ-2 fizetés esedékes 2026.10.30, összeg: ", empty.Message);
        Assert.Contains(notes, note => note.Invoice == "SZ-3" && note.Message == "SZ-3 fizetés esedékes 2026.10.30, összeg: ");
        Assert.Contains(notes, note => note.Identity == "paid|222|SZ-3" && note.Message.Contains("igen", StringComparison.Ordinal));
        Assert.DoesNotContain(notes, note => note.Invoice == "SZ-7");
        Assert.DoesNotContain(notes, note => note.Invoice == "SZ-4" && note.Type == "Payment");
        Assert.DoesNotContain(notes, note => note.Invoice is "SZ-5" or "SZ-6");
        Assert.DoesNotContain(notes, note => note.Message.Contains("bármi", StringComparison.Ordinal));
        Assert.Equal(2, notes.Count(note => note.Invoice == "SZ-2"));
    }

    [Fact]
    public void Calendar_shifts_the_weekend_and_collapses_notice_days()
    {
        var sundayDeadline = Invoice("Partner", "111", "SZ-1", "2026-10-25", "10", "Átutalás");
        var monday = NoticeRules.Compose(Monday, [sundayDeadline], "1,2");
        var payment = Assert.Single(monday, note => note.Type == "Payment");
        Assert.StartsWith("2026-10-26|", payment.Identity, StringComparison.Ordinal);
        Assert.Contains("2026.10.25", payment.Message, StringComparison.Ordinal);

        var sunday = NoticeRules.Compose(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero), [sundayDeadline], "7,5,1");
        Assert.DoesNotContain(sunday, note => note.Type == "Payment");

        var missed = NoticeRules.Compose(
            new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero),
            [Invoice("Partner", "111", "SZ-1", "2026-10-26", "10", "Átutalás")],
            "7,5,1");
        var tuesday = Assert.Single(missed, note => note.Type == "Payment");
        Assert.StartsWith("2026-10-27|", tuesday.Identity, StringComparison.Ordinal);
        Assert.DoesNotContain(missed, note => note.Identity.StartsWith("2026-10-26|", StringComparison.Ordinal));

        var collapsed = NoticeRules.Compose(
            new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
            [Invoice("Partner", "111", "SZ-1", "2026-10-30", "10", "Átutalás")],
            "7, 7, 1");
        Assert.Single(collapsed, note => note.Type == "Payment");
        Assert.DoesNotContain(collapsed, note => note.Identity == "notice-days");

        var due = NoticeRules.Compose(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero), [Invoice("Partner", "111", "SZ-1", "2026-10-30", "10", "Átutalás")], "7,0,1");
        Assert.Single(due, note => note.Type == "Payment");
        var error = Assert.Single(due, note => note.Identity == "notice-days");
        Assert.Equal("Az Előértesítési napok értéke hibás: 7,0,1.", error.Message);
        var log = new MemoryNotificationLog();
        foreach (var note in due)
        {
            log.Add(note);
        }

        foreach (var note in NoticeRules.Compose(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero), [Invoice("Partner", "111", "SZ-1", "2026-10-30", "10", "Átutalás")], "9,x"))
        {
            log.Add(note);
        }

        Assert.Equal("Az Előértesítési napok értéke hibás: 7,0,1.", Assert.Single(log.All, note => note.Identity == "notice-days").Message);

        var early = NoticeRules.Compose(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero), [Invoice("Partner", "111", "SZ-9", "2026-10-30", "10", "Átutalás")], "7");
        var onDay = NoticeRules.Compose(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero), [Invoice("Partner", "111", "SZ-9", "2026-10-30", "10", "Átutalás")], "7");
        var late = NoticeRules.Compose(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero), [Invoice("Partner", "111", "SZ-9", "2026-10-30", "10", "Átutalás")], "7");
        Assert.Equal(early.Single(note => note.Type == "Payment").Message, onDay.Single(note => note.Type == "Payment").Message);
        Assert.Equal(onDay.Single(note => note.Type == "Payment").Message, late.Single(note => note.Type == "Payment").Message);

        var budapestMonday = NoticeRules.Compose(
            new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero),
            [Invoice("Partner", "111", "SZ-8", "2026-10-26", "10", "Átutalás")],
            "7,5,1");
        Assert.Contains(budapestMonday, note => note.Type == "Payment" && note.Identity.StartsWith("2026-10-26|", StringComparison.Ordinal));

        var history = new MemoryNotificationLog();
        history.Add(NotificationRecords.PaymentNote(new DateOnly(2026, 10, 26), "111", "Partner", "SZ-1", new DateOnly(2026, 10, 25), "10,00"));
        foreach (var note in NoticeRules.Compose(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), [sundayDeadline], "7,5,1"))
        {
            history.Add(note);
        }

        Assert.Equal(2, history.All.Count(note => note.Type == "Payment"));
    }

    [Fact]
    public void Storno_chain_blocks_payment_and_keeps_the_paid_error()
    {
        var chained = NoticeRules.Compose(
            Monday,
            [
                Invoice("Partner", "111", "EREDETI", "2026-10-26", "10", "Átutalás", related: "SZ-1;STORNO:SZ-3"),
                Invoice("Partner", "111", "SZ-1", "2026-10-26", "10", "Átutalás", original: "EREDETI"),
                Invoice("Partner", "111", "SZ-3", "2026-10-26", "10", "Átutalás", original: "EREDETI", paid: "ROSSZ"),
                Invoice("Partner", "111", "KIVUL", "2026-10-26", "10", "Átutalás", original: "MAS", paid: "ROSSZ"),
                Invoice("Partner", "999", "MASIK", "2026-10-26", "10", "Átutalás", original: "EREDETI"),
                Invoice("Partner", "111", "KESZPENZ", "2026-10-26", "10", "Készpénz", paid: "X")
            ],
            "7,5,1");
        Assert.DoesNotContain(chained, note => note.Type == "Payment" && note.Invoice is "EREDETI" or "SZ-1" or "SZ-3");
        Assert.Contains(chained, note => note.Type == "Payment" && note.Invoice == "KIVUL");
        Assert.Contains(chained, note => note.Type == "Payment" && note.Invoice == "MASIK");
        Assert.Contains(chained, note => note.Identity == "paid|111|SZ-3");
        Assert.Contains(chained, note => note.Identity == "paid|111|KIVUL");
        Assert.Contains(chained, note => note.Identity == "paid|111|KESZPENZ");
        Assert.DoesNotContain(chained, note => note.Type == "Payment" && note.Invoice == "KESZPENZ");

        var open = NoticeRules.Compose(
            Monday,
            [
                Invoice("Partner", "111", "EREDETI", "2026-10-26", "10", "Átutalás", related: "SZ-1;SZ-3"),
                Invoice("Partner", "111", "SZ-1", "2026-10-26", "10", "Átutalás", original: "EREDETI"),
                Invoice("Partner", "111", "SZ-3", "2026-10-26", "10", "Átutalás", original: "EREDETI")
            ],
            "7,5,1");
        Assert.Contains(open, note => note.Type == "Payment" && note.Invoice == "EREDETI");
        Assert.Contains(open, note => note.Type == "Payment" && note.Invoice == "SZ-1");
        Assert.Contains(open, note => note.Type == "Payment" && note.Invoice == "SZ-3");
    }

    [Fact]
    public async Task Run_stops_for_a_bad_start_date_and_a_missing_previous_file()
    {
        var unread = new FakeNotices();
        var log = new MemoryNotificationLog();
        await NotificationComposition.Run("2026.03.01", Monday, unread, log);
        Assert.Equal(0, unread.Reads);
        var start = Assert.Single(log.All);
        Assert.Equal("start-date", start.Identity);
        Assert.Equal("A kezdődátum hiányzik vagy hibás.", start.Message);
        Assert.Equal("", start.Taxnumber);

        var current = Workbook(2026, "7,5,1", Row("SZ-1", "2026-10-26", "1500.5"));
        var startup = new MemoryNotificationLog();
        await NotificationComposition.Run(
            "2026-01-01",
            Monday,
            new FakeNotices
            {
                Result = Read(2026, [2025], new Dictionary<int, byte[]> { [2026] = current }, "7,5,1")
            },
            startup);
        var payment = Assert.Single(startup.All, note => note.Type == "Payment");
        Assert.Equal("SZ-1 fizetés esedékes 2026.10.26, összeg: 1500,50", payment.Message);
        Assert.DoesNotContain(startup.All, note => note.Identity.StartsWith("missing-file", StringComparison.Ordinal));

        var previous = Workbook(2025, "3", Row("REGI", "2026-10-26", "10"));
        var both = new MemoryNotificationLog();
        await NotificationComposition.Run(
            "2026-01-01",
            Monday,
            new FakeNotices
            {
                Result = Read(2026, [], new Dictionary<int, byte[]> { [2025] = previous, [2026] = WorkbookFile.Create(2026, 1, "2026-01-01T00:00:00Z", "7,5,1") }, "7,5,1")
            },
            both);
        Assert.Contains(both.All, note => note.Type == "Payment" && note.Invoice == "REGI");

        var later = new MemoryNotificationLog();
        await NotificationComposition.Run(
            "2026-01-01",
            new DateTimeOffset(2027, 1, 4, 12, 0, 0, TimeSpan.Zero),
            new FakeNotices
            {
                Result = Read(2027, [2026], new Dictionary<int, byte[]> { [2027] = Workbook(2027, "7,5,1", Row("SZ-1", "2027-01-04", "10")) }, "7,5,1")
            },
            later);
        var missingPrevious = Assert.Single(later.All);
        Assert.Equal("missing-file|SZÁMLÁK2026.xlsx", missingPrevious.Identity);
        Assert.Equal("Az előző évi nyilvántartás hiányzik: SZÁMLÁK2026.xlsx. A feldolgozás megállt.", missingPrevious.Message);

        var absent = new MemoryNotificationLog();
        await NotificationComposition.Run(
            "2026-01-01",
            Monday,
            new FakeNotices { Result = Read(2026, [2026], new Dictionary<int, byte[]>(), "7,5,1") },
            absent);
        Assert.Equal(
            "A tárgyévi nyilvántartás hiányzik: SZÁMLÁK2026.xlsx. A feldolgozás megállt.",
            Assert.Single(absent.All).Message);
    }

    [Fact]
    public async Task Read_failure_names_the_current_file_and_keeps_an_existing_error()
    {
        var log = new MemoryNotificationLog();
        var existing = NotificationRecords.OutOfYearNote(
            InvoiceIdentity.Create("12345678", "REGI"),
            "Partner",
            new DateOnly(2024, 1, 2),
            new DateOnly(2024, 1, 16),
            null);
        log.Add(existing);
        var source = new FakeNotices
        {
            Result = Read(2026, [], new Dictionary<int, byte[]>(), "7,5,1", failed: [2026, 2025])
        };
        await NotificationComposition.Run("2026-01-01", Monday, source, log);
        await NotificationComposition.Run("2026-01-01", Monday, source, log);
        var failure = Assert.Single(log.All, note => note.Identity == "read|2026-10-26");
        Assert.Equal("A nyilvántartás olvasása megállt: SZÁMLÁK2026.xlsx.", failure.Message);
        Assert.Equal("", failure.Taxnumber);
        Assert.DoesNotContain(log.All, note => note.Type == "Payment");
        Assert.Equal(existing.Message, Assert.Single(log.All, note => note.Identity == existing.Identity).Message);

        await NotificationComposition.Run("2026-01-01", new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), source, log);
        Assert.Contains(log.All, note => note.Identity == "read|2026-10-27");

        var previousOnly = new MemoryNotificationLog();
        await NotificationComposition.Run(
            "2026-01-01",
            Monday,
            new FakeNotices { Result = Read(2026, [], new Dictionary<int, byte[]> { [2026] = WorkbookFile.Create(2026, 1, "2026-01-01T00:00:00Z", "7,5,1") }, "7,5,1", failed: [2025]) },
            previousOnly);
        Assert.Equal("A nyilvántartás olvasása megállt: SZÁMLÁK2025.xlsx.", Assert.Single(previousOnly.All).Message);

        var composition = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "NotificationComposition.cs")));
        var rules = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "NoticeRules.cs")));
        Assert.DoesNotContain("Document", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("Document", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_entry_has_no_trigger_and_does_not_send()
    {
        foreach (var file in Directory.GetFiles(FindRepoFile("src"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("TimerTrigger", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpTrigger", source, StringComparison.Ordinal);
        }

        var composition = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "NotificationComposition.cs")));
        var program = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "Program.cs")));
        Assert.DoesNotContain("Slack", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("NAV", composition, StringComparison.Ordinal);
        Assert.Contains("FunctionIds.NotificationComposition", program, StringComparison.Ordinal);
        Assert.Contains("NotificationComposition.RunConfigured", program, StringComparison.Ordinal);
        Assert.Contains("TableNotificationLog.Open", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Slack", program, StringComparison.Ordinal);
    }

    private static NoticeInvoice Invoice(
        string partner,
        string tax,
        string number,
        string deadline,
        string amount,
        string method,
        string issue = "2026-10-01",
        string original = "",
        string related = "",
        string present = "",
        string paid = "")
    {
        return new NoticeInvoice
        {
            Partner = partner,
            TaxNumber = tax,
            InvoiceNumber = number,
            OriginalInvoice = original,
            Related = related,
            IssueDate = issue,
            Deadline = deadline,
            Amount = amount,
            PaymentMethod = method,
            Present = present,
            Paid = paid
        };
    }

    private static string[] Row(string invoice, string deadline, string amount)
    {
        var cells = Enumerable.Repeat("", 16).ToArray();
        cells[0] = "Partner";
        cells[1] = "12345678";
        cells[3] = invoice;
        cells[6] = "2026-10-01";
        cells[7] = deadline;
        cells[8] = amount;
        cells[11] = "Átutalás";
        return cells;
    }

    private static byte[] Workbook(int year, string noticeDays, string[] row)
    {
        return WorkbookFile.Apply(
            WorkbookFile.Create(year, 1, $"{year}-01-01T00:00:00Z", noticeDays),
            new RegistryEdit
            {
                Year = year,
                CurrentYear = year,
                SheetMonth = 1,
                NewRows = [new RegistryRowWrite { Month = 1, Cells = row }]
            });
    }

    private static NoticeRead Read(int current, int[] missing, Dictionary<int, byte[]> files, string noticeDays, int[]? failed = null)
    {
        return new NoticeRead
        {
            CurrentYear = current,
            MissingYears = missing,
            FailedYears = failed ?? [],
            Files = files,
            NoticeDays = noticeDays
        };
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class FakeNotices : INoticeSource
    {
        public int Reads { get; private set; }

        public NoticeRead Result { get; init; } = new()
        {
            CurrentYear = 2026,
            MissingYears = [],
            FailedYears = [],
            Files = new Dictionary<int, byte[]>(),
            NoticeDays = "7,5,1"
        };

        public Task<NoticeRead> Read(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Result);
        }
    }
}
