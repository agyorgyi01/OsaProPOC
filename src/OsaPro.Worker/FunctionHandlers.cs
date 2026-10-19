using Microsoft.Extensions.Logging;
using OsaPro.Common;

namespace OsaPro.Worker;

public static class FunctionHandlers
{
    public static void RunInvoiceProcessing(ILogger logger)
    {
        Run(FunctionIds.InvoiceProcessing, logger, work: null);
    }

    public static void RunNotificationComposition(ILogger logger)
    {
        Run(FunctionIds.NotificationComposition, logger, work: null);
    }

    internal static void RunInvoiceProcessing(ILogger logger, Action work)
    {
        Run(FunctionIds.InvoiceProcessing, logger, work);
    }

    internal static void RunNotificationComposition(ILogger logger, Action work)
    {
        Run(FunctionIds.NotificationComposition, logger, work);
    }

    private static void Run(string functionId, ILogger logger, Action? work)
    {
        logger.LogInformation("Futás kezdete. {FunctionId}", functionId);

        try
        {
            work?.Invoke();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Futási hiba. {FunctionId}", functionId);
            throw;
        }
    }
}
