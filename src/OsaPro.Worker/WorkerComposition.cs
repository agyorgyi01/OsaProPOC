using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsaPro.Common;

namespace OsaPro.Worker;

public static class WorkerComposition
{
    public const string UserSecretsId = "8f4e2c1a-6b7d-4e9f-a1c2-3d4e5f607182";

    public static IHost Build(string contentRoot, bool development, string? userSecretsId)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = contentRoot,
            EnvironmentName = development ? Environments.Development : Environments.Production,
            ApplicationName = "OsaPro.Worker"
        });

        if (development && !string.IsNullOrWhiteSpace(userSecretsId))
        {
            builder.Configuration.AddUserSecrets(userSecretsId);
        }

        var masker = SecretMasker.FromConfiguration(builder.Configuration);
        var consoleFactory = LoggerFactory.Create(logging => logging.AddConsole());
        builder.Services.AddSingleton(consoleFactory);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new MaskingLoggerProvider(masker, consoleFactory.CreateLogger));

        return builder.Build();
    }
}
