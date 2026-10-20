namespace OsaPro.Common.Nav;

public sealed class NavQueryException : Exception
{
    public NavQueryException(string message)
        : base(message)
    {
    }

    public NavQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
