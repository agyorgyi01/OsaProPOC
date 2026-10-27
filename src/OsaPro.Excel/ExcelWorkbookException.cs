namespace OsaPro.Excel;

public class ExcelWorkbookException : Exception
{
    public ExcelWorkbookException(string message)
        : base(message)
    {
    }

    public ExcelWorkbookException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public sealed class MissingInsDateException : ExcelWorkbookException
{
    public MissingInsDateException()
        : base("Az InsDate hiányzik.")
    {
    }
}
