using Microsoft.Extensions.Configuration;

namespace OsaPro.Common;

public sealed class SecretMasker
{
    private readonly IReadOnlyList<(string Value, string Token)> _replacements;

    private SecretMasker(IReadOnlyList<(string Value, string Token)> replacements)
    {
        _replacements = replacements;
    }

    public static SecretMasker FromConfiguration(IConfiguration configuration)
    {
        var replacements = new List<(string Value, string Token)>();
        var seenValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var child in configuration.GetSection(SecretConfiguration.SectionName).GetChildren())
        {
            if (string.IsNullOrEmpty(child.Value) || !seenValues.Add(child.Value))
            {
                continue;
            }

            replacements.Add((child.Value, $"##{child.Key}##"));
        }

        replacements.Sort(static (left, right) => right.Value.Length.CompareTo(left.Value.Length));
        return new SecretMasker(replacements);
    }

    public string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text) || _replacements.Count == 0)
        {
            return text ?? string.Empty;
        }

        foreach (var (value, token) in _replacements)
        {
            text = text.Replace(value, token, StringComparison.Ordinal);
        }

        return text;
    }
}
