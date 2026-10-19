using Microsoft.Extensions.Configuration;

namespace OsaPro.Common;

public static class SecretConfiguration
{
    public const string SectionName = "Secrets";

    public static void AddSources(
        IConfigurationBuilder builder,
        string contentRoot,
        bool includeUserSecrets,
        string? userSecretsId)
    {
        builder.SetBasePath(contentRoot);
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.AddEnvironmentVariables();

        if (includeUserSecrets && !string.IsNullOrWhiteSpace(userSecretsId))
        {
            builder.AddUserSecrets(userSecretsId);
        }
    }

    public static IReadOnlyList<string> SecretKeys(IConfiguration configuration)
    {
        return configuration
            .GetSection(SectionName)
            .GetChildren()
            .Select(child => child.Key)
            .ToArray();
    }

    public static bool IsSecretKey(IConfiguration configuration, string key)
    {
        return SecretKeys(configuration).Contains(key, StringComparer.Ordinal);
    }
}
