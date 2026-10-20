using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace OsaPro.NAV;

internal sealed class NavInvoiceSender : IDisposable
{
    private readonly NavOptions _options;
    private readonly HttpClient _client;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public NavInvoiceSender(NavOptions options, HttpMessageHandler handler, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _options = options;
        _client = new HttpClient(handler, disposeHandler: false);
        _delay = delay;
    }

    public async Task<string> PostAsync(string operation, string body, CancellationToken cancellationToken)
    {
        Exception? last = null;
        var attempts = 1 + Math.Max(0, _options.RetryCount);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
            {
                await _delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken).ConfigureAwait(false);
            }

            var result = await SendOnceAsync(operation, body, cancellationToken).ConfigureAwait(false);
            if (result.Body is not null)
            {
                return result.Body;
            }

            last = result.Error;
        }

        throw new NavQueryException("A NAV-hívás átmeneti hiba után sem sikerült.", last!);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<SendResult> SendOnceAsync(string operation, string body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(operation));
            request.Content = new StringContent(body, Encoding.UTF8, "application/xml");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (status == 429 || status >= 500)
            {
                return SendResult.Transient(new HttpRequestException($"HTTP {status}"));
            }

            if (status >= 400)
            {
                throw new NavQueryException($"A NAV HTTP {status} választ adott.");
            }

            if (status != 200)
            {
                throw new NavQueryException($"A NAV HTTP {status} választ adott.");
            }

            var funcCode = ReadFuncCode(text);
            if (funcCode is null)
            {
                throw new NavQueryException("A NAV-válaszból hiányzik a funcCode.");
            }

            if (!string.Equals(funcCode, "OK", StringComparison.Ordinal))
            {
                throw new NavQueryException($"A NAV funcCode {funcCode} választ adott.");
            }

            return SendResult.Success(text);
        }
        catch (NavQueryException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            return SendResult.Transient(exception);
        }
        catch (TimeoutException exception)
        {
            return SendResult.Transient(exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return SendResult.Transient(exception);
        }
    }

    private Uri Endpoint(string operation)
    {
        var root = _options.BaseUrl.TrimEnd('/');
        return new Uri($"{root}/invoiceService/v3/{operation}", UriKind.Absolute);
    }

    private static string? ReadFuncCode(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            return document
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "funcCode")
                ?.Value
                .Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private sealed record SendResult(string? Body, Exception? Error)
    {
        public static SendResult Success(string body) => new(body, null);

        public static SendResult Transient(Exception error) => new(null, error);
    }
}
