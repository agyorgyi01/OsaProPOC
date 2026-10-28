using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OsaPro.Excel;

public sealed class ExcelWorkbook : IDisposable
{
    private const string GraphHost = "graph.microsoft.com";

    private readonly ExcelOptions _options;
    private readonly HttpClient _client;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private string? _accessToken;
    private int _year;
    private int _month;
    private string? _file;

    public ExcelWorkbook(
        ExcelOptions options,
        HttpMessageHandler handler,
        Func<DateTimeOffset> clock,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _options = options;
        _client = new HttpClient(handler, disposeHandler: false);
        _clock = clock;
        _delay = delay ?? ((span, token) => Task.Delay(span, token));
    }

    public async Task EnsureMonth(int year, int month, CancellationToken cancellationToken = default)
    {
        WorkbookRules.RequireMonth(month);
        WorkbookRules.RequireHandledYear(year, _clock());
        WorkbookRules.RequireAddress(_options);
        _year = year;
        _month = month;
        _file = WorkbookRules.FileName(year);
        try
        {
            await EnsureCore(cancellationToken).ConfigureAwait(false);
        }
        catch (ExcelWorkbookException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failed("A művelet sikertelen.", exception);
        }
    }

    internal async Task<RegistrySnapshot> ReadRegistry(CancellationToken cancellationToken = default)
    {
        WorkbookRules.RequireAddress(_options);
        _year = WorkbookRules.CurrentYear(_clock());
        _month = WorkbookRules.CurrentMonth(_clock());
        _file = "";
        try
        {
            var files = await DownloadHandledFiles(cancellationToken).ConfigureAwait(false);
            return WorkbookFile.ReadSnapshot(files, _clock());
        }
        catch (ExcelWorkbookException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failed("A művelet sikertelen.", exception);
        }
    }

    internal async Task UploadFiles(
        IReadOnlyDictionary<int, byte[]> files,
        int currentYear,
        CancellationToken cancellationToken = default)
    {
        WorkbookRules.RequireAddress(_options);
        _year = currentYear;
        _month = WorkbookRules.CurrentMonth(_clock());
        _file = "";
        try
        {
            await Token(cancellationToken).ConfigureAwait(false);
            var siteId = await SiteId(cancellationToken).ConfigureAwait(false);
            var folderId = await FolderId(siteId, cancellationToken).ConfigureAwait(false);
            var children = await Children(siteId, folderId, cancellationToken).ConfigureAwait(false);
            foreach (var (year, bytes) in files.OrderBy(pair => pair.Key == currentYear).ThenBy(pair => pair.Key))
            {
                _year = year;
                _file = WorkbookRules.FileName(year);
                var existing = Match(children, _file);
                if (existing is null)
                {
                    var path = EncodeRelative($"{_options.FolderPath}/{_file}");
                    var url = $"https://{GraphHost}/v1.0/sites/{Uri.EscapeDataString(siteId)}/drive/root:/{path}:/content?@microsoft.graph.conflictBehavior=fail";
                    using var response = await Send(() => Put(url, bytes), authorize: true, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw Failed($"A fájl létrehozása nem sikerült. Állapot: {(int)response.StatusCode}.");
                    }
                }
                else
                {
                    _file = existing.Name;
                    await Upload(siteId, existing.Id, bytes, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (ExcelWorkbookException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failed("A művelet sikertelen.", exception);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<Dictionary<int, byte[]>> DownloadHandledFiles(CancellationToken cancellationToken)
    {
        await Token(cancellationToken).ConfigureAwait(false);
        var siteId = await SiteId(cancellationToken).ConfigureAwait(false);
        var folderId = await FolderId(siteId, cancellationToken).ConfigureAwait(false);
        var children = await Children(siteId, folderId, cancellationToken).ConfigureAwait(false);
        var files = new Dictionary<int, byte[]>();
        foreach (var year in new[] { _year, _year - 1 })
        {
            var child = Match(children, WorkbookRules.FileName(year));
            if (child is null)
            {
                continue;
            }

            files[year] = await Download(siteId, child.Id, cancellationToken).ConfigureAwait(false);
        }

        return files;
    }

    internal async Task<NoticeRead> ReadForNotices(CancellationToken cancellationToken = default)
    {
        WorkbookRules.RequireAddress(_options);
        var current = WorkbookRules.CurrentYear(_clock());
        _year = current;
        _month = WorkbookRules.CurrentMonth(_clock());
        _file = "";
        try
        {
            return await ReadNotices(current, cancellationToken).ConfigureAwait(false);
        }
        catch (ExcelWorkbookException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw Failed("A művelet sikertelen.", exception);
        }
    }

    private async Task<NoticeRead> ReadNotices(int current, CancellationToken cancellationToken)
    {
        await Token(cancellationToken).ConfigureAwait(false);
        var siteId = await SiteId(cancellationToken).ConfigureAwait(false);
        var folderId = await FolderId(siteId, cancellationToken).ConfigureAwait(false);
        var children = await Children(siteId, folderId, cancellationToken).ConfigureAwait(false);
        var files = new Dictionary<int, byte[]>();
        var missing = new List<int>();
        var failed = new List<int>();
        foreach (var year in new[] { current, current - 1 })
        {
            var child = Match(children, WorkbookRules.FileName(year));
            if (child is null)
            {
                missing.Add(year);
                continue;
            }

            _year = year;
            _file = child.Name;
            try
            {
                files[year] = await Download(siteId, child.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed.Add(year);
            }
        }

        var noticeDays = files.TryGetValue(current, out var currentBytes)
            ? WorkbookFile.ReadNoticeDays(currentBytes)
            : WorkbookRules.DefaultNoticeDays;
        return new NoticeRead
        {
            CurrentYear = current,
            MissingYears = missing,
            FailedYears = failed,
            Files = files,
            NoticeDays = noticeDays
        };
    }

    private async Task EnsureCore(CancellationToken cancellationToken)
    {
        await Token(cancellationToken).ConfigureAwait(false);
        var siteId = await SiteId(cancellationToken).ConfigureAwait(false);
        var folderId = await FolderId(siteId, cancellationToken).ConfigureAwait(false);
        var children = await Children(siteId, folderId, cancellationToken).ConfigureAwait(false);
        var existing = Match(children, _file!);
        if (existing is null)
        {
            if (await TryCreate(siteId, children, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            children = await Children(siteId, folderId, cancellationToken).ConfigureAwait(false);
            existing = Match(children, _file!) ?? throw Failed("A foglalt nevű fájl nem olvasható.");
        }

        await RepairExisting(siteId, children, existing, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryCreate(
        string siteId,
        IReadOnlyList<Child> children,
        CancellationToken cancellationToken)
    {
        var rows = await PreviousRows(siteId, children, cancellationToken).ConfigureAwait(false);
        var parameters = WorkbookRules.ResolveParameters(rows ?? [], _year);
        var bytes = WorkbookFile.Create(_year, _month, parameters.InsDate, parameters.NoticeDays);
        var path = EncodeRelative($"{_options.FolderPath}/{_file}");
        var url = $"https://{GraphHost}/v1.0/sites/{Uri.EscapeDataString(siteId)}/drive/root:/{path}:/content?@microsoft.graph.conflictBehavior=fail";
        using var response = await Send(() => Put(url, bytes), authorize: true, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        throw Failed($"A fájl létrehozása nem sikerült. Állapot: {(int)response.StatusCode}.");
    }

    private async Task RepairExisting(
        string siteId,
        IReadOnlyList<Child> children,
        Child existing,
        CancellationToken cancellationToken)
    {
        _file = existing.Name;
        await Checkout(siteId, existing.Id, cancellationToken).ConfigureAwait(false);
        var bytes = await Download(siteId, existing.Id, cancellationToken).ConfigureAwait(false);
        var missingParameters = WorkbookFile.ReadParameterRows(bytes) is null;
        var insDate = "";
        var noticeDays = "";
        if (missingParameters)
        {
            var rows = await PreviousRows(siteId, children, cancellationToken).ConfigureAwait(false);
            var parameters = WorkbookRules.ResolveParameters(rows ?? [], _year);
            insDate = parameters.InsDate;
            noticeDays = parameters.NoticeDays;
        }

        byte[] repaired;
        try
        {
            repaired = WorkbookFile.Repair(bytes, _month, missingParameters, insDate, noticeDays);
        }
        catch (Exception exception)
        {
            throw Failed("A helyi módosítás nem sikerült.", exception);
        }

        await Upload(siteId, existing.Id, repaired, cancellationToken).ConfigureAwait(false);
        await CheckIn(siteId, existing.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<(string Key, string Value)>?> PreviousRows(
        string siteId,
        IReadOnlyList<Child> children,
        CancellationToken cancellationToken)
    {
        var name = WorkbookRules.FileName(_year - 1);
        var previous = children.FirstOrDefault(child => WorkbookRules.SameName(child.Name, name));
        if (previous is null)
        {
            return null;
        }

        var bytes = await Download(siteId, previous.Id, cancellationToken).ConfigureAwait(false);
        return WorkbookFile.ReadParameterRows(bytes) ?? [];
    }

    private async Task<string> SiteId(CancellationToken cancellationToken)
    {
        var path = _options.SitePath.StartsWith('/') ? _options.SitePath : "/" + _options.SitePath;
        var url = $"https://{GraphHost}/v1.0/sites/{Uri.EscapeDataString(_options.SiteHost)}:/{EncodeRelative(path)}";
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, url), authorize: true, cancellationToken).ConfigureAwait(false);
        using var document = await Json(response, "A webhely nem található.").ConfigureAwait(false);
        return Required(document.RootElement, "id");
    }

    private async Task<string> FolderId(string siteId, CancellationToken cancellationToken)
    {
        var url = $"https://{GraphHost}/v1.0/sites/{Uri.EscapeDataString(siteId)}/drive/root:/{EncodeRelative(_options.FolderPath)}";
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, url), authorize: true, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw Failed("A mappa nem található.");
        }

        using var document = await Json(response, "A mappa nem olvasható.").ConfigureAwait(false);
        return Required(document.RootElement, "id");
    }

    private async Task<IReadOnlyList<Child>> Children(string siteId, string folderId, CancellationToken cancellationToken)
    {
        var url = $"https://{GraphHost}/v1.0/sites/{Uri.EscapeDataString(siteId)}/drive/items/{Uri.EscapeDataString(folderId)}/children";
        var children = new List<Child>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrWhiteSpace(url))
        {
            if (!seen.Add(url))
            {
                throw Failed("A mappa lapozása ismétlődik.");
            }

            var page = url;
            using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, page), authorize: true, cancellationToken).ConfigureAwait(false);
            using var document = await Json(response, "A mappa tartalma nem olvasható.").ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                        && item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                        && id.GetString() is string itemId && name.GetString() is string itemName)
                    {
                        children.Add(new Child(itemId, itemName));
                    }
                }
            }

            url = document.RootElement.TryGetProperty("@odata.nextLink", out var link) && link.ValueKind == JsonValueKind.String
                ? link.GetString()
                : null;
        }

        return children;
    }

    private async Task Checkout(string siteId, string itemId, CancellationToken cancellationToken)
    {
        var url = Item(siteId, itemId) + "/checkout";
        Exception? last = null;
        var attempts = Attempts();
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
            {
                await _delay(Delay(), cancellationToken).ConfigureAwait(false);
            }

            try
            {
                using var request = Authorized(new HttpRequestMessage(HttpMethod.Post, url));
                using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return;
                }

                last = Failed($"A foglalás nem sikerült. Állapot: {(int)response.StatusCode}.");
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken))
            {
                last = exception;
            }
        }

        throw Failed("A foglalás nem sikerült.", last);
    }

    private async Task<byte[]> Download(string siteId, string itemId, CancellationToken cancellationToken)
    {
        var url = Item(siteId, itemId) + "/content";
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, url), authorize: true, cancellationToken).ConfigureAwait(false);
        if (IsRedirect(response.StatusCode))
        {
            var location = response.Headers.Location ?? throw Failed("A letöltési cím hiányzik.");
            var absolute = location.IsAbsoluteUri ? location : new Uri(response.RequestMessage?.RequestUri!, location);
            using var followed = await Send(() => new HttpRequestMessage(HttpMethod.Get, absolute), authorize: false, cancellationToken).ConfigureAwait(false);
            return await Bytes(followed, "A letöltés nem sikerült.").ConfigureAwait(false);
        }

        return await Bytes(response, "A letöltés nem sikerült.").ConfigureAwait(false);
    }

    private async Task Upload(string siteId, string itemId, byte[] bytes, CancellationToken cancellationToken)
    {
        var url = Item(siteId, itemId) + "/content";
        using var response = await Send(() => Put(url, bytes), authorize: true, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw Failed($"A feltöltés nem sikerült. Állapot: {(int)response.StatusCode}.");
        }
    }

    private async Task CheckIn(string siteId, string itemId, CancellationToken cancellationToken)
    {
        var url = Item(siteId, itemId) + "/checkin";
        using var response = await Send(
            () => new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent("{}", Encoding.UTF8, "application/json") },
            authorize: true,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            throw Failed($"A visszaadás nem sikerült. Állapot: {(int)response.StatusCode}.");
        }
    }

    private async Task Token(CancellationToken cancellationToken)
    {
        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.TenantId)}/oauth2/v2.0/token";
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = "https://graph.microsoft.com/.default"
            })
        }, authorize: false, cancellationToken).ConfigureAwait(false);
        using var document = await Json(response, "A Graph-belépés nem sikerült.").ConfigureAwait(false);
        _accessToken = Required(document.RootElement, "access_token");
    }

    private async Task<HttpResponseMessage> Send(
        Func<HttpRequestMessage> create,
        bool authorize,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        var attempts = Attempts();
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
            {
                await _delay(Delay(), cancellationToken).ConfigureAwait(false);
            }

            try
            {
                using var request = authorize ? Authorized(create()) : create();
                var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var code = (int)response.StatusCode;
                if (code == 429 || code >= 500)
                {
                    last = Failed($"Átmeneti válasz. Állapot: {code}.");
                    response.Dispose();
                    continue;
                }

                return response;
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken))
            {
                last = exception;
            }
        }

        throw Failed("A Graph-hívás nem sikerült.", last);
    }

    private HttpRequestMessage Authorized(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return request;
    }

    private static HttpRequestMessage Put(string url, byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
    }

    private async Task<JsonDocument> Json(HttpResponseMessage response, string failure)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw Failed($"{failure} Állapot: {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw Failed(failure, exception);
        }
    }

    private async Task<byte[]> Bytes(HttpResponseMessage response, string failure)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw Failed($"{failure} Állapot: {(int)response.StatusCode}.");
        }

        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    private ExcelWorkbookException Failed(string detail, Exception? inner = null)
    {
        var file = string.IsNullOrWhiteSpace(_file) ? "" : $" Fájl: {_file}.";
        var message = $"{detail} Év: {_year}. Hónap: {_month}.{file}";
        return inner is null ? new ExcelWorkbookException(message) : new ExcelWorkbookException(message, inner);
    }

    private int Attempts()
    {
        return 1 + Math.Max(0, _options.RetryCount);
    }

    private TimeSpan Delay()
    {
        return TimeSpan.FromSeconds(Math.Max(0, _options.RetryDelaySeconds));
    }

    private static bool IsRedirect(HttpStatusCode status)
    {
        return status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is TaskCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is HttpRequestException or TimeoutException or TaskCanceledException;
    }

    private static string Item(string siteId, string itemId)
    {
        return $"https://{GraphHost}/v1.0/sites/{Uri.EscapeDataString(siteId)}/drive/items/{Uri.EscapeDataString(itemId)}";
    }

    private static string EncodeRelative(string relative)
    {
        return string.Join(
            "/",
            relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static Child? Match(IReadOnlyList<Child> children, string name)
    {
        return children.FirstOrDefault(child => WorkbookRules.SameName(child.Name, name));
    }

    private static string Required(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new ExcelWorkbookException($"A Graph-válaszból hiányzik a {name}.");
        }

        return property.GetString()!;
    }

    private sealed record Child(string Id, string Name);
}
