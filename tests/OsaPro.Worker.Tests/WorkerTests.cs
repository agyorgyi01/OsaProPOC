using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsaPro.Common;
using OsaPro.Worker;

namespace OsaPro.Worker.Tests;

public class WorkerTests
{
    [Fact]
    public void Host_builds_as_functions_worker_with_empty_secrets_and_has_no_trigger()
    {
        var contentRoot = Directory.CreateTempSubdirectory("osapro-worker-");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{ "Secrets": {} }""");

        using var host = WorkerComposition.Build(contentRoot.FullName, development: false, userSecretsId: null);
        Assert.NotNull(host.Services.GetService<IOptions<WorkerOptions>>());
        Assert.Empty(SecretConfiguration.SecretKeys(host.Services.GetRequiredService<IConfiguration>()));

        var attributes = typeof(FunctionHandlers).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMembers())
            .SelectMany(member => member.GetCustomAttributes(inherit: true));

        Assert.DoesNotContain(
            attributes,
            attribute => attribute.GetType().Name is "TimerTriggerAttribute" or "HttpTriggerAttribute");
        var handlerSource = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "FunctionHandlers.cs")));
        Assert.DoesNotContain("InboundInvoiceQuery", handlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TimerTrigger", handlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpTrigger", handlerSource, StringComparison.Ordinal);
        foreach (var file in Directory.GetFiles(FindRepoFile("src"), "*.cs", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("tokenExchange", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Handlers_log_their_own_function_id_for_success_and_failure()
    {
        AssertLoggedId(
            FunctionIds.InvoiceProcessing,
            logger => FunctionHandlers.RunInvoiceProcessing(logger),
            logger => FunctionHandlers.RunInvoiceProcessing(logger, () => throw new InvalidOperationException("hiba")));
        AssertLoggedId(
            FunctionIds.NotificationComposition,
            logger => FunctionHandlers.RunNotificationComposition(logger),
            logger => FunctionHandlers.RunNotificationComposition(logger, () => throw new InvalidOperationException("hiba")));
    }

    [Fact]
    public void Invoke_runs_only_the_selected_handler_in_development()
    {
        var invoice = Capture(FunctionIds.InvoiceProcessing, isDevelopment: true);
        Assert.Equal(WorkerEntry.Success, invoice.ExitCode);
        Assert.Contains(invoice.Ids, id => id == FunctionIds.InvoiceProcessing);
        Assert.DoesNotContain(FunctionIds.NotificationComposition, invoice.Ids);
        Assert.DoesNotContain(FunctionIds.SlackSending, invoice.Ids);

        var outsideDevelopment = Capture(FunctionIds.InvoiceProcessing, isDevelopment: false);
        Assert.Equal(WorkerEntry.Success, outsideDevelopment.ExitCode);
        Assert.Empty(outsideDevelopment.Ids);

        var unknown = Capture("nincs-ilyen", isDevelopment: true);
        Assert.Equal(WorkerEntry.Failure, unknown.ExitCode);
        Assert.Empty(unknown.Ids);
    }

    [Fact]
    public void Development_host_reads_user_secrets()
    {
        var attribute = typeof(WorkerComposition).Assembly
            .GetCustomAttributes<UserSecretsIdAttribute>()
            .Single();
        Assert.Equal(WorkerComposition.UserSecretsId, attribute.UserSecretsId);

        var secretsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            "osapro-worker-test-secrets");
        Directory.CreateDirectory(secretsDirectory);
        var secretsPath = Path.Combine(secretsDirectory, "secrets.json");
        var previous = File.Exists(secretsPath) ? File.ReadAllText(secretsPath) : null;
        var contentRoot = Directory.CreateTempSubdirectory("osapro-worker-secrets-");
        File.WriteAllText(Path.Combine(contentRoot.FullName, "appsettings.json"), """{ "Secrets": {} }""");

        try
        {
            File.WriteAllText(
                secretsPath,
                """
                {
                  "Secrets": {
                    "Excel": "user-secret-value"
                  }
                }
                """);

            foreach (var development in new[] { true, false })
            {
                using var host = WorkerComposition.Build(
                    contentRoot.FullName,
                    development,
                    userSecretsId: "osapro-worker-test-secrets");
                var configuration = host.Services.GetRequiredService<IConfiguration>();

                Assert.Equal("user-secret-value", configuration["Secrets:Excel"]);
            }
            Assert.DoesNotContain("user-secret-value", File.ReadAllText(Path.Combine(contentRoot.FullName, "appsettings.json")), StringComparison.Ordinal);
            Assert.DoesNotContain(
                "user-secret-value",
                File.ReadAllText(FindVersionedAppSettings()),
                StringComparison.Ordinal);
        }
        finally
        {
            if (previous is null)
            {
                File.Delete(secretsPath);
            }
            else
            {
                File.WriteAllText(secretsPath, previous);
            }
        }
    }

    private static void AssertLoggedId(string functionId, Action<CapturingLogger> success, Action<CapturingLogger> failure)
    {
        var successLogger = new CapturingLogger();
        success(successLogger);
        AssertOwnId(successLogger, functionId);

        var failureLogger = new CapturingLogger();
        Assert.Throws<InvalidOperationException>(() => failure(failureLogger));
        AssertOwnId(failureLogger, functionId);
    }

    private static void AssertOwnId(CapturingLogger logger, string functionId)
    {
        Assert.Contains(logger.Ids, id => id == functionId);
        foreach (var other in FunctionIds.All.Where(id => id != functionId))
        {
            Assert.DoesNotContain(other, logger.Ids);
        }
    }

    private static (int ExitCode, IReadOnlyList<string> Ids) Capture(string functionId, bool isDevelopment)
    {
        var logger = new CapturingLogger();
        var exitCode = WorkerEntry.Execute(["--invoke", functionId], isDevelopment, logger);
        return (exitCode, logger.Ids);
    }

    private static string FindVersionedAppSettings()
    {
        return FindRepoFile(Path.Combine("src", "OsaPro.Worker", "appsettings.json"));
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Ids { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
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
            if (state is not IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return;
            }

            foreach (var pair in pairs)
            {
                if (pair.Key == "FunctionId" && pair.Value is string id)
                {
                    Ids.Add(id);
                }
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
