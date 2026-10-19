using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsaPro.Worker;

var development = IsDevelopment();
var plan = WorkerEntry.Plan(args, development);

if (plan.ExitCode != WorkerEntry.Success)
{
    return plan.ExitCode;
}

var host = WorkerComposition.Build(
    contentRoot: AppContext.BaseDirectory,
    development: development,
    userSecretsId: WorkerComposition.UserSecretsId);

if (plan.FunctionId is not null)
{
    var logger = host.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("OsaPro.Worker");
    return WorkerEntry.Execute(args, development, logger);
}

host.Run();
return WorkerEntry.Success;

static bool IsDevelopment()
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    return string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase);
}
