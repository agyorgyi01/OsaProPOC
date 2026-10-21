namespace OsaPro.Excel;

public sealed class ExcelWorkbookException : Exception
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
