namespace OsaPro.Common;

public static class FunctionIds
{
    public const string InvoiceProcessing = "szamlafeldolgozas";

    public const string NotificationComposition = "ertesites-osszeallitas";

    public const string SlackSending = "slack-uzenetek-kuldese";

    public static IReadOnlyList<string> All { get; } =
    [
        InvoiceProcessing,
        NotificationComposition,
        SlackSending
    ];
}
