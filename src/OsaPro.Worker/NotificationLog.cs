using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;

namespace OsaPro.Worker;

internal interface INotificationLog
{
    void Add(NotificationNote note);

    bool HasPaymentOn(DateOnly day);
}

internal sealed class NotificationNote
{
    public const string ErrorType = "Error";

    public const string DocumentType = "Document";

    public const string PaymentType = "Payment";

    public const string IdleType = "Idle";

    public const string NotificationType = "Notification";

    public required string Identity { get; init; }

    public required string Type { get; init; }

    public required string Message { get; init; }

    public string Taxnumber { get; init; } = "";

    public string Partnername { get; init; } = "";

    public string Invoice { get; init; } = "";
}

internal sealed class MemoryNotificationLog : INotificationLog
{
    private readonly Dictionary<string, NotificationNote> _notes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<NotificationNote> All => _notes.Values;

    public void Add(NotificationNote note)
    {
        if (_notes.ContainsKey(note.Identity))
        {
            return;
        }

        _notes[note.Identity] = note;
    }

    public bool HasPaymentOn(DateOnly day)
    {
        var prefix = NotificationRecords.DayKey(day) + "|";
        foreach (var note in _notes.Values)
        {
            if (note.Type == NotificationNote.PaymentType && note.Identity.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class TableNotificationLog : INotificationLog
{
    public const string TableName = "notification";

    private readonly TableClient _client;

    public static INotificationLog Open(string? tableUri)
    {
        if (string.IsNullOrWhiteSpace(tableUri))
        {
            throw new ProcessingException("A NotificationTableUri hiányzik.");
        }

        return new TableNotificationLog(tableUri);
    }

    public TableNotificationLog(string tableUri)
    {
        _client = new TableClient(new Uri(tableUri), TableName, new DefaultAzureCredential());
    }

    public void Add(NotificationNote note)
    {
        var fields = NotificationRecords.TableFields(note);
        var entity = new TableEntity(fields["PartitionKey"], fields["RowKey"]);
        foreach (var pair in fields)
        {
            if (pair.Key is "PartitionKey" or "RowKey")
            {
                continue;
            }

            entity[pair.Key] = pair.Value;
        }

        try
        {
            _client.AddEntity(entity);
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
        }
    }

    public bool HasPaymentOn(DateOnly day)
    {
        var prefix = NotificationRecords.DayKey(day) + "|";
        var next = NotificationRecords.DayKey(day.AddDays(1)) + "|";
        var filter = $"PartitionKey eq '{NotificationNote.PaymentType}' and RowKey ge '{prefix}' and RowKey lt '{next}'";
        return _client.Query<TableEntity>(filter, maxPerPage: 1, select: ["RowKey"]).Any();
    }
}
