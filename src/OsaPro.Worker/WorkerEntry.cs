using Microsoft.Extensions.Logging;
using OsaPro.Common;

namespace OsaPro.Worker;

public static class WorkerEntry
{
    public const int Success = 0;

    public const int Failure = 1;

    public static int Execute(string[] args, bool isDevelopment, ILogger logger, Action? invoiceWork = null)
    {
        var plan = Plan(args, isDevelopment);
        if (plan.ExitCode != Success)
        {
            return plan.ExitCode;
        }

        if (plan.FunctionId == FunctionIds.InvoiceProcessing)
        {
            if (invoiceWork is null)
            {
                FunctionHandlers.RunInvoiceProcessing(logger);
            }
            else
            {
                FunctionHandlers.RunInvoiceProcessing(logger, invoiceWork);
            }
        }
        else if (plan.FunctionId == FunctionIds.NotificationComposition)
        {
            FunctionHandlers.RunNotificationComposition(logger);
        }

        return Success;
    }

    internal static InvokePlan Plan(string[] args, bool isDevelopment)
    {
        var index = Array.IndexOf(args, "--invoke");
        if (index < 0 || index + 1 >= args.Length)
        {
            return new InvokePlan(FunctionId: null, StartHost: true, ExitCode: Success);
        }

        if (!isDevelopment)
        {
            return new InvokePlan(FunctionId: null, StartHost: true, ExitCode: Success);
        }

        var functionId = args[index + 1];
        if (functionId is FunctionIds.InvoiceProcessing or FunctionIds.NotificationComposition)
        {
            return new InvokePlan(functionId, StartHost: false, ExitCode: Success);
        }

        return new InvokePlan(FunctionId: null, StartHost: false, ExitCode: Failure);
    }
}

internal readonly record struct InvokePlan(string? FunctionId, bool StartHost, int ExitCode);
