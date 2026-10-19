using Microsoft.Extensions.Logging;

namespace OsaPro.Common;

public sealed class MaskingLogger : ILogger
{
    private readonly ILogger _inner;
    private readonly SecretMasker _masker;

    public MaskingLogger(ILogger inner, SecretMasker masker)
    {
        _inner = inner;
        _masker = masker;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = _masker.Mask(formatter(state, exception));
        if (TryMaskPairs(state, out var maskedState))
        {
            _inner.Log(logLevel, eventId, maskedState, exception, (_, _) => message);
            return;
        }

        _inner.Log(logLevel, eventId, state, exception, (_, _) => message);
    }

    private bool TryMaskPairs<TState>(
        TState state,
        out IReadOnlyList<KeyValuePair<string, object?>> maskedState)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            maskedState = [];
            return false;
        }

        maskedState = values
            .Select(pair => new KeyValuePair<string, object?>(
                pair.Key,
                pair.Value is string text ? _masker.Mask(text) : pair.Value))
            .ToArray();
        return true;
    }
}
