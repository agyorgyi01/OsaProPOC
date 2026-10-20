namespace OsaPro.NAV;

public sealed class InvoiceDigestItem
{
    public required string InvoiceNumber { get; init; }

    public int? BatchIndex { get; init; }

    public required string InvoiceOperation { get; init; }

    public required string InvoiceCategory { get; init; }

    public required DateOnly InvoiceIssueDate { get; init; }

    public required string SupplierTaxNumber { get; init; }

    public string? SupplierGroupMemberTaxNumber { get; init; }

    public required string SupplierName { get; init; }

    public string? CustomerTaxNumber { get; init; }

    public string? CustomerGroupMemberTaxNumber { get; init; }

    public string? CustomerName { get; init; }

    public string? PaymentMethod { get; init; }

    public DateOnly? PaymentDate { get; init; }

    public string? InvoiceAppearance { get; init; }

    public string? Source { get; init; }

    public DateOnly? InvoiceDeliveryDate { get; init; }

    public string? Currency { get; init; }

    public decimal? InvoiceNetAmount { get; init; }

    public decimal? InvoiceNetAmountHuf { get; init; }

    public decimal? InvoiceVatAmount { get; init; }

    public decimal? InvoiceVatAmountHuf { get; init; }

    public string? TransactionId { get; init; }

    public int? Index { get; init; }

    public string? OriginalInvoiceNumber { get; init; }

    public int? ModificationIndex { get; init; }

    public required DateTimeOffset InsDate { get; init; }

    public bool? CompletenessIndicator { get; init; }
}
