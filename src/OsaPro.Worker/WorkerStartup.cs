using OsaPro.Common;

namespace OsaPro.Worker;

public readonly record struct WorkerStartup(string? EnvironmentName, bool IncludeUserSecrets)
{
    public const string InvalidEnvironmentMessage = "Ismeretlen környezetnév. A host nem indul.";

    public bool IsValid => EnvironmentName is not null;

    public bool IsDevelopment => EnvironmentName == HostEnvironment.Development;

    public int? ExitCode => IsValid ? null : WorkerEntry.Failure;

    public static WorkerStartup Prepare(
        string? azureFunctionsEnvironment,
        string? dotnetEnvironment,
        string? websiteInstanceId)
    {
        if (!HostEnvironment.TryResolve(azureFunctionsEnvironment, dotnetEnvironment, out var name))
        {
            return new WorkerStartup(null, false);
        }

        return new WorkerStartup(name, HostEnvironment.IncludeUserSecrets(websiteInstanceId));
    }
}
