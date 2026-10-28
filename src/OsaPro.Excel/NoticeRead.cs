namespace OsaPro.Excel;

internal sealed class NoticeRead
{
    public required int CurrentYear { get; init; }

    public required IReadOnlyList<int> MissingYears { get; init; }

    public required IReadOnlyList<int> FailedYears { get; init; }

    public required IReadOnlyDictionary<int, byte[]> Files { get; init; }

    public required string NoticeDays { get; init; }
}
