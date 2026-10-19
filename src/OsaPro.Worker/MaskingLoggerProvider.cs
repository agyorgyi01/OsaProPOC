using Microsoft.Extensions.Logging;
using OsaPro.Common;

namespace OsaPro.Worker;

public sealed class MaskingLoggerProvider : ILoggerProvider
{
    private readonly SecretMasker _masker;
    private readonly Func<string, ILogger> _innerFactory;

    public MaskingLoggerProvider(SecretMasker masker, Func<string, ILogger> innerFactory)
    {
        _masker = masker;
        _innerFactory = innerFactory;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new MaskingLogger(_innerFactory(categoryName), _masker);
    }

    public void Dispose()
    {
    }
}
