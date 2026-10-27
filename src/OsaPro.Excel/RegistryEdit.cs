namespace OsaPro.Excel;

internal sealed class RegistrySnapshot
{
    public bool HasFile { get; init; }

    public string InsDateText { get; init; } = "";

    public string NoticeDays { get; init; } = "";

    public int CursorYear { get; init; }

    public IReadOnlyList<StoredInvoiceRow> Rows { get; init; } = [];

    public IReadOnlyDictionary<int, byte[]> Files { get; init; } = new Dictionary<int, byte[]>();
}

internal sealed class StoredInvoiceRow
{
    public int Year { get; init; }

    public int Month { get; init; }

    public uint RowIndex { get; init; }

    public required string[] Cells { get; init; }
}

internal sealed class RegistryEdit
{
    public int Year { get; init; }

    public int CurrentYear { get; init; }

    public int SheetMonth { get; init; }

    public string CarriedInsDate { get; init; } = "";

    public string CarriedNoticeDays { get; init; } = WorkbookRules.DefaultNoticeDays;

    public bool WriteInsDate { get; init; }

    public string? InsDate { get; init; }

    public IReadOnlyList<RegistryRowWrite> NewRows { get; init; } = [];

    public IReadOnlyList<RegistryLinkWrite> Links { get; init; } = [];
}

internal sealed class RegistryRowWrite
{
    public int Month { get; init; }

    public required string[] Cells { get; init; }
}

internal sealed class RegistryLinkWrite
{
    public required string TaxNumber { get; init; }

    public required string InvoiceNumber { get; init; }

    public required string Related { get; init; }
}
