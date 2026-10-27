using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsaPro.Common;
using OsaPro.Worker;

var startup = WorkerStartup.Prepare(
    Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"),
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
    Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

if (startup.ExitCode is int exitCode)
{
    Console.Error.WriteLine(WorkerStartup.InvalidEnvironmentMessage);
    return exitCode;
}

var plan = WorkerEntry.Plan(args, startup.IsDevelopment);

if (plan.ExitCode != WorkerEntry.Success)
{
    return plan.ExitCode;
}

var host = WorkerComposition.Build(
    contentRoot: AppContext.BaseDirectory,
    environmentName: startup.EnvironmentName!,
    userSecretsId: WorkerComposition.UserSecretsId,
    includeUserSecrets: startup.IncludeUserSecrets);

if (plan.FunctionId is not null)
{
    var logger = host.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("OsaPro.Worker");
    Action? invoiceWork = plan.FunctionId == FunctionIds.InvoiceProcessing
        ? () => InvoiceProcessing.RunConfigured(host.Services.GetRequiredService<IConfiguration>()).GetAwaiter().GetResult()
        : null;
    return WorkerEntry.Execute(args, startup.IsDevelopment, logger, invoiceWork);
}

host.Run();
return WorkerEntry.Success;
