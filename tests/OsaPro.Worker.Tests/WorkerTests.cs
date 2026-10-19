using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsaPro.Common;
using OsaPro.Worker;

namespace OsaPro.Worker.Tests;

public class WorkerTests
{
    [Fact]
    public async Task Host_starts_with_empty_secrets_and_has_no_trigger()
    {
        var contentRoot = Directory.CreateTempSubdirectory("osapro-worker-");
        await File.WriteAllTextAsync(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{ "Secrets": {} }""");

        using var host = WorkerComposition.Build(contentRoot.FullName, development: false, userSecretsId: null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);

        var attributes = typeof(FunctionHandlers).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMembers())
            .SelectMany(member => member.GetCustomAttributes(inherit: true));

        Assert.DoesNotContain(
            attributes,
            attribute => attribute.GetType().Name is "TimerTriggerAttribute" or "HttpTriggerAttribute");
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
                    "ExcelFile": "user-secret-value"
                  }
                }
                """);

            using var host = WorkerComposition.Build(
                contentRoot.FullName,
                development: true,
                userSecretsId: "osapro-worker-test-secrets");
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            Assert.Equal("user-secret-value", configuration["Secrets:ExcelFile"]);
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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "OsaPro.Worker", "appsettings.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("A verziókezelt appsettings.json nem található.");
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
