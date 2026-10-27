namespace OsaPro.Worker;

internal sealed class ProcessingException : Exception
{
    public ProcessingException(string message)
        : base(message)
    {
    }

    public ProcessingException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
