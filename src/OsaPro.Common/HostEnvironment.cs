namespace OsaPro.Common;

public static class HostEnvironment
{
    public const string Development = "Development";

    public const string Test = "Test";

    public const string Production = "Production";

    private static readonly string[] CanonicalNames = [Development, Test, Production];

    public static bool TryResolve(string? azureFunctionsEnvironment, string? dotnetEnvironment, out string name)
    {
        var raw = Normalize(azureFunctionsEnvironment) ?? Normalize(dotnetEnvironment);
        if (raw is null)
        {
            name = Production;
            return true;
        }

        foreach (var canonical in CanonicalNames)
        {
            if (canonical.Equals(raw, StringComparison.OrdinalIgnoreCase))
            {
                name = canonical;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }

    public static bool IncludeUserSecrets(string? websiteInstanceId)
    {
        return string.IsNullOrWhiteSpace(websiteInstanceId);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
