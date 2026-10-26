using Microsoft.Extensions.Configuration;
using OsaPro.Common;

namespace OsaPro.Common.Tests;

public class EnvironmentConfigurationTests
{
    [Fact]
    public void Azure_functions_environment_wins_over_dotnet_environment()
    {
        var resolved = HostEnvironment.TryResolve("Test", "Development", out var name);

        Assert.True(resolved);
        Assert.Equal(HostEnvironment.Test, name);
    }

    [Fact]
    public void Dotnet_environment_is_used_when_function_environment_is_missing()
    {
        var resolved = HostEnvironment.TryResolve(null, "Development", out var name);

        Assert.True(resolved);
        Assert.Equal(HostEnvironment.Development, name);
    }

    [Fact]
    public void Missing_variables_select_production()
    {
        var resolved = HostEnvironment.TryResolve("  ", null, out var name);

        Assert.True(resolved);
        Assert.Equal(HostEnvironment.Production, name);
    }

    [Fact]
    public void Environment_name_match_is_case_insensitive()
    {
        var resolved = HostEnvironment.TryResolve(null, " test ", out var name);

        Assert.True(resolved);
        Assert.Equal(HostEnvironment.Test, name);
    }

    [Fact]
    public void Unknown_environment_name_is_invalid()
    {
        var resolved = HostEnvironment.TryResolve("Staging", "Development", out var name);

        Assert.False(resolved);
        Assert.Equal(string.Empty, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Local_host_includes_user_secrets(string? websiteInstanceId)
    {
        Assert.True(HostEnvironment.IncludeUserSecrets(websiteInstanceId));
    }

    [Fact]
    public void Azure_host_omits_user_secrets()
    {
        Assert.False(HostEnvironment.IncludeUserSecrets("instance-1"));
    }

    [Fact]
    public void Environment_file_overrides_base_and_later_sources_win()
    {
        var contentRoot = Directory.CreateTempSubdirectory("osapro-env-");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{ "Sample": { "Value": "base" } }""");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.Development.json"),
            """{ "Sample": { "Value": "overlay" } }""");
        var secretsId = "osapro-common-env-order";
        var secretsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            secretsId);
        Directory.CreateDirectory(secretsDirectory);
        var secretsPath = Path.Combine(secretsDirectory, "secrets.json");
        var previousSecrets = File.Exists(secretsPath) ? File.ReadAllText(secretsPath) : null;

        try
        {
            var fromFile = Build(contentRoot.FullName, includeUserSecrets: false, userSecretsId: null, HostEnvironment.Development);
            Assert.Equal("overlay", fromFile["Sample:Value"]);

            Environment.SetEnvironmentVariable("Sample__Value", "from-env");
            var fromEnvironment = Build(contentRoot.FullName, includeUserSecrets: false, userSecretsId: null, HostEnvironment.Development);
            Assert.Equal("from-env", fromEnvironment["Sample:Value"]);

            File.WriteAllText(
                secretsPath,
                """{ "Sample": { "Value": "from-secret" } }""");
            var fromSecrets = Build(contentRoot.FullName, includeUserSecrets: true, secretsId, HostEnvironment.Development);
            Assert.Equal("from-secret", fromSecrets["Sample:Value"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Sample__Value", null);
            if (previousSecrets is null)
            {
                File.Delete(secretsPath);
            }
            else
            {
                File.WriteAllText(secretsPath, previousSecrets);
            }
        }
    }

    [Fact]
    public void Missing_environment_file_keeps_the_base_value()
    {
        var contentRoot = Directory.CreateTempSubdirectory("osapro-env-missing-");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{ "Sample": { "Value": "base" } }""");

        var configuration = Build(contentRoot.FullName, includeUserSecrets: false, userSecretsId: null, HostEnvironment.Production);

        Assert.Equal("base", configuration["Sample:Value"]);
    }

    [Fact]
    public void Default_environment_ignores_process_dotnet_environment()
    {
        var contentRoot = Directory.CreateTempSubdirectory("osapro-env-default-");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{ "Sample": { "Value": "base" } }""");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.Test.json"),
            """{ "Sample": { "Value": "overlay" } }""");
        var previous = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", HostEnvironment.Test);

        try
        {
            var builder = new ConfigurationBuilder();
            SecretConfiguration.AddSources(builder, contentRoot.FullName, includeUserSecrets: false, userSecretsId: null);
            var configuration = builder.Build();

            Assert.Equal("base", configuration["Sample:Value"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", previous);
        }
    }

    private static IConfiguration Build(
        string contentRoot,
        bool includeUserSecrets,
        string? userSecretsId,
        string environmentName)
    {
        var builder = new ConfigurationBuilder();
        SecretConfiguration.AddSources(builder, contentRoot, includeUserSecrets, userSecretsId, environmentName);
        return builder.Build();
    }
}
