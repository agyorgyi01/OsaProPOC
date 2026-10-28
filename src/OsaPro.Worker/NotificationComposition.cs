using System.Globalization;
using Microsoft.Extensions.Configuration;
using OsaPro.Excel;

namespace OsaPro.Worker;

internal interface INoticeSource
{
    Task<NoticeRead> Read(CancellationToken cancellationToken);
}

internal sealed class ExcelNoticeSource(ExcelWorkbook workbook) : INoticeSource
{
    public Task<NoticeRead> Read(CancellationToken cancellationToken)
    {
        return workbook.ReadForNotices(cancellationToken);
    }
}

internal static class NotificationComposition
{
    public static async Task RunConfigured(
        IConfiguration configuration,
        INotificationLog notifications,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        using var handler = new HttpClientHandler();
        using var workbook = new ExcelWorkbook(
            ExcelOptions.FromConfiguration(configuration),
            handler,
            () => now);
        await Run(
            configuration["Processing:StartDate"],
            now,
            new ExcelNoticeSource(workbook),
            notifications,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task Run(
        string? startDate,
        DateTimeOffset now,
        INoticeSource source,
        INotificationLog notifications,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParseExact(
                startDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var start))
        {
            notifications.Add(NotificationRecords.StartDateNote());
            return;
        }

        var today = WorkbookRules.CurrentDate(now);
        NoticeRead read;
        try
        {
            read = await source.Read(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            notifications.Add(NotificationRecords.ReadErrorNote(today, today.Year));
            return;
        }

        if (read.MissingYears.Contains(read.CurrentYear))
        {
            notifications.Add(NotificationRecords.MissingFileNote(read.CurrentYear, currentYear: true));
            return;
        }

        var previous = read.CurrentYear - 1;
        if (read.MissingYears.Contains(previous) && today.Year != start.Year)
        {
            notifications.Add(NotificationRecords.MissingFileNote(previous, currentYear: false));
            return;
        }

        if (read.FailedYears.Count > 0)
        {
            var year = read.FailedYears.Contains(read.CurrentYear) ? read.CurrentYear : read.FailedYears[0];
            notifications.Add(NotificationRecords.ReadErrorNote(today, year));
            return;
        }

        var invoices = new List<NoticeInvoice>();
        foreach (var (year, bytes) in read.Files.OrderBy(pair => pair.Key))
        {
            if (year != read.CurrentYear && year != previous)
            {
                continue;
            }

            foreach (var row in WorkbookFile.ReadNoticeRows(bytes, year))
            {
                invoices.Add(Map(row));
            }
        }

        foreach (var note in NoticeRules.Compose(now, invoices, read.NoticeDays, notifications.HasPaymentOn(today)))
        {
            notifications.Add(note);
        }
    }

    private static NoticeInvoice Map(StoredInvoiceRow row)
    {
        return new NoticeInvoice
        {
            Partner = Cell(row, 0),
            TaxNumber = Cell(row, 1),
            InvoiceNumber = Cell(row, 3),
            OriginalInvoice = Cell(row, 4),
            Related = Cell(row, 5),
            IssueDate = Cell(row, 6),
            Deadline = Cell(row, 7),
            Amount = Cell(row, 8),
            PaymentMethod = Cell(row, 11),
            Present = Cell(row, 12),
            Paid = Cell(row, 13)
        };
    }

    private static string Cell(StoredInvoiceRow row, int index)
    {
        return index < row.Cells.Length ? row.Cells[index] : "";
    }
}
