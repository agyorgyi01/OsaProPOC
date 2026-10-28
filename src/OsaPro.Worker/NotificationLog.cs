using Azure;
using Azure.Data.Tables;
using Azure.Identity;

namespace OsaPro.Worker;

internal interface INotificationLog
{
    void Add(NotificationNote note);
}

internal sealed class NotificationNote
{
    public const string ErrorType = "Error";

    public const string DocumentType = "Document";

    public const string PaymentType = "Payment";

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
}
