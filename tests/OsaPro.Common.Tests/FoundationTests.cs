using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OsaPro.Common;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OsaPro.Common.Tests;

public class FoundationTests
{
    [Fact]
    public void Function_ids_are_distinct()
    {
        var ids = FunctionIds.All;

        Assert.Equal(3, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(FunctionIds.InvoiceProcessing, ids);
        Assert.Contains(FunctionIds.NotificationComposition, ids);
        Assert.Contains(FunctionIds.SlackSending, ids);
    }

    [Fact]
    public void Empty_secrets_section_builds()
    {
        var contentRoot = CreateContentRoot("""{ "Secrets": {} }""");
        var configuration = Build(contentRoot, includeUserSecrets: false, userSecretsId: null);

        Assert.Empty(SecretConfiguration.SecretKeys(configuration));
    }

    [Fact]
    public void In_memory_secret_key_is_recognized_and_absent_from_versioned_file()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Excel"] = "valami.xls"
            })
            .Build();

        Assert.True(SecretConfiguration.IsSecretKey(configuration, "Excel"));
        Assert.DoesNotContain("valami.xls", ReadVersionedAppSettings(), StringComparison.Ordinal);
    }

    [Fact]
    public void Development_source_list_includes_user_secrets()
    {
        var contentRoot = CreateContentRoot("""{ "Secrets": {} }""");
        var builder = new ConfigurationBuilder();
        SecretConfiguration.AddSources(builder, contentRoot, includeUserSecrets: true, userSecretsId: "test-user-secrets");

        Assert.Contains(
            builder.Sources,
            source => string.Equals(
                source.GetType().GetProperty("Path")?.GetValue(source) as string,
                "secrets.json",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Environment_variable_maps_to_secret_key()
    {
        var contentRoot = CreateContentRoot("""{ "Secrets": {} }""");
        Environment.SetEnvironmentVariable("Secrets__Excel", "env-secret");
        try
        {
            var configuration = Build(contentRoot, includeUserSecrets: false, userSecretsId: null);

            Assert.Equal("env-secret", configuration["Secrets:Excel"]);
            Assert.True(SecretConfiguration.IsSecretKey(configuration, "Excel"));
            Assert.DoesNotContain("env-secret", ReadVersionedAppSettings(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Secrets__Excel", null);
        }
    }

    [Fact]
    public void Masks_secret_value_in_message_and_structured_state()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Excel"] = "valami.xls",
                ["Secrets:Empty"] = ""
            })
            .Build();
        var logger = CreateLogger(configuration);

        logger.LogInformation("nem nyitható meg a {Path} fájl", "valami.xls");

        var entry = logger.Entries.Single();
        Assert.Equal("nem nyitható meg a ##Excel## fájl", entry.Message);
        Assert.DoesNotContain("valami.xls", entry.Message, StringComparison.Ordinal);
        Assert.Contains(entry.State, pair => pair.Value is "##Excel##");
        Assert.DoesNotContain(entry.State, pair => pair.Value is string text && text.Contains("valami.xls", StringComparison.Ordinal));
        Assert.Equal("abc", SecretMasker.FromConfiguration(configuration).Mask("abc"));
        Assert.Equal("VALAMI.XLS", SecretMasker.FromConfiguration(configuration).Mask("VALAMI.XLS"));
    }

    [Fact]
    public void Masks_secret_value_in_exception_text()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Excel"] = "valami.xls"
            })
            .Build();
        var logger = CreateLogger(configuration);

        logger.LogError(new InvalidOperationException("nem nyitható meg a valami.xls fájl"), "hiba");

        var entry = logger.Entries.Single();
        Assert.Contains("##Excel##", entry.ExceptionText, StringComparison.Ordinal);
        Assert.DoesNotContain("valami.xls", entry.ExceptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_secret_value_uses_the_first_key_name()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:First"] = "same-value",
                ["Secrets:Second"] = "same-value"
            })
            .Build();
        var logger = CreateLogger(configuration);

        logger.LogInformation("{Value}", "same-value");

        var entry = logger.Entries.Single();
        Assert.Equal("##First##", entry.Message);
        Assert.DoesNotContain("same-value", entry.Message, StringComparison.Ordinal);
        Assert.Contains(entry.State, pair => pair.Value is "##First##");
    }

    private static CapturingLogger CreateLogger(IConfiguration configuration)
    {
        var inner = new CapturingLogger();
        return new CapturingLogger(new MaskingLogger(inner, SecretMasker.FromConfiguration(configuration)), inner);
    }

    private static IConfiguration Build(string contentRoot, bool includeUserSecrets, string? userSecretsId)
    {
        var builder = new ConfigurationBuilder();
        SecretConfiguration.AddSources(builder, contentRoot, includeUserSecrets, userSecretsId);
        return builder.Build();
    }

    private static string CreateContentRoot(string appSettings)
    {
        var directory = Directory.CreateTempSubdirectory("osapro-common-");
        File.WriteAllText(Path.Combine(directory.FullName, "appsettings.json"), appSettings);
        return directory.FullName;
    }

    private static string ReadVersionedAppSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "OsaPro.Worker", "appsettings.json");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("A verziókezelt appsettings.json nem található.");
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ILogger? _forward;
        private readonly CapturingLogger _store;

        public CapturingLogger()
        {
            _store = this;
        }

        public CapturingLogger(ILogger forward, CapturingLogger store)
        {
            _forward = forward;
            _store = store;
        }

        public List<LogEntry> Entries => _store._entries;

        private readonly List<LogEntry> _entries = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _forward?.BeginScope(state) ?? NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (_forward is not null)
            {
                _forward.Log(logLevel, eventId, state, exception, formatter);
                return;
            }

            var pairs = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            _entries.Add(new LogEntry(formatter(state, exception), pairs.ToArray(), exception?.ToString()));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed record LogEntry(
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> State,
        string? ExceptionText);
}
