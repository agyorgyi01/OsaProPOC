using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OsaPro.Excel;

namespace OsaPro.Excel.Tests;

public class ExcelWorkbookTests
{
    private static readonly DateTimeOffset June2026 = new(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public async Task Transient_failure_retries_without_sleeping_and_a_client_error_does_not()
    {
        var delays = new List<TimeSpan>();
        var handler = new GraphDouble { TokenFailures = 3 };
        using var workbook = Create(handler, delays, Options());
        var started = DateTime.UtcNow;

        await workbook.EnsureMonth(2026, 5);

        Assert.Equal(4, handler.TokenCalls);
        Assert.Equal(3, delays.Count);
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromSeconds(5), delay));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
        Assert.Contains(handler.Calls, call => call.Url.Contains("/sites/contoso.sharepoint.com:/sites/OsaPro", StringComparison.Ordinal));
        Assert.Contains(handler.Calls, call => call.Url.Contains("/drive/root:/Szamlak/SZÁMLÁK2026.xlsx:/content?@microsoft.graph.conflictBehavior=fail", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("/sites/site-1/drive", StringComparison.Ordinal) && !call.Url.Contains("/sites/site-1/drive/", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("MasikMappa", StringComparison.Ordinal));
        Assert.Contains("grant_type=client_credentials", handler.TokenBody, StringComparison.Ordinal);
        Assert.Contains("client_id=client-1", handler.TokenBody, StringComparison.Ordinal);
        Assert.Contains("client_secret=secret-1", handler.TokenBody, StringComparison.Ordinal);
        Assert.Contains("scope=https%3A%2F%2Fgraph.microsoft.com%2F.default", handler.TokenBody, StringComparison.Ordinal);
        Assert.Contains(handler.Calls, call => call.Url == "https://login.microsoftonline.com/tenant-1/oauth2/v2.0/token");

        var rejected = new GraphDouble { SiteStatus = HttpStatusCode.Conflict };
        using var rejectedWorkbook = Create(rejected, [], Options());
        var error = await Assert.ThrowsAsync<ExcelWorkbookException>(() => rejectedWorkbook.EnsureMonth(2026, 5));
        Assert.Contains("409", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, rejected.Calls.Count(call => call.Url.Contains("/sites/contoso.sharepoint.com:/sites/OsaPro", StringComparison.Ordinal)));
        Assert.DoesNotContain(rejected.Calls, call => call.Url.Contains("/drive", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Download_failure_retries_without_starting_checkout_again()
    {
        var delays = new List<TimeSpan>();
        var handler = new GraphDouble { DownloadFailures = 1 };
        handler.Add("SZÁMLÁK2026.xlsx", WorkbookFile.Create(2026, 5, "kept", "7,5,1"));
        using var workbook = Create(handler, delays, Options());

        await workbook.EnsureMonth(2026, 5);

        Assert.Equal(1, handler.Calls.Count(call => call.Url.EndsWith("/checkout", StringComparison.Ordinal)));
        Assert.Equal(1, delays.Count);
        var download = handler.Calls.Single(call => call.Url.StartsWith("https://download.test/", StringComparison.Ordinal));
        Assert.Null(download.Authorization);
        Assert.Contains(handler.Calls, call => call.Url.EndsWith("/content", StringComparison.Ordinal) && call.Authorization == "Bearer token-1");
    }

    [Fact]
    public async Task Children_on_the_second_page_are_found_in_the_configured_folder()
    {
        var handler = new GraphDouble { SplitPages = true };
        handler.Add("SZÁMLÁK2026.xlsx", WorkbookFile.Create(2026, 5, "kept", "7,5,1"));
        using var workbook = Create(handler, [], Options());

        await workbook.EnsureMonth(2026, 5);

        Assert.Contains(handler.Calls, call => call.Url.Contains("/children?page=2", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("conflictBehavior", StringComparison.Ordinal));
        Assert.Equal("SZÁMLÁK2026.xlsx", Assert.Single(handler.Files.Keys));
    }

    [Fact]
    public async Task Missing_folder_fails_without_a_create_call()
    {
        var handler = new GraphDouble { FolderExists = false };
        using var workbook = Create(handler, [], Options());

        await Assert.ThrowsAsync<ExcelWorkbookException>(() => workbook.EnsureMonth(2026, 5));

        Assert.DoesNotContain(handler.Calls, call => call.Method is "PUT" or "PATCH");
        Assert.DoesNotContain(handler.Calls, call => call.Method == "POST" && call.Url.Contains("graph.microsoft.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Empty_address_invalid_month_and_outside_year_make_no_call()
    {
        var handler = new GraphDouble();
        var month = new GraphDouble();
        var year = new GraphDouble();
        using var workbook = Create(handler, [], new ExcelOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" });
        await Assert.ThrowsAsync<ExcelWorkbookException>(() => workbook.EnsureMonth(2026, 5));
        await Assert.ThrowsAsync<ExcelWorkbookException>(() => Create(month, [], Options()).EnsureMonth(2026, 13));
        await Assert.ThrowsAsync<ExcelWorkbookException>(() => Create(year, [], Options()).EnsureMonth(2024, 5));
        Assert.Empty(handler.Calls);
        Assert.Empty(month.Calls);
        Assert.Empty(year.Calls);
    }

    [Fact]
    public async Task New_file_is_uploaded_without_checkout_and_an_existing_sheet_keeps_its_name()
    {
        var created = new GraphDouble();
        using (var workbook = Create(created, [], Options()))
        {
            await workbook.EnsureMonth(2026, 5);
        }

        var uploaded = created.Files["SZÁMLÁK2026.xlsx"];
        using var document = SpreadsheetDocument.Open(new MemoryStream(uploaded), false);
        var names = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value).ToList();
        Assert.Equal(["Paraméterek", "Május"], names);
        Assert.Contains(created.Calls, call => call.Url.Contains("conflictBehavior=fail", StringComparison.Ordinal));
        Assert.DoesNotContain(created.Calls, call => call.Url.Contains("/checkout", StringComparison.Ordinal) || call.Url.Contains("createSession", StringComparison.Ordinal));

        var existing = new GraphDouble();
        existing.Add("számlák2026.xlsx", Rename(WorkbookFile.Create(2026, 5, "kept", "7,5,1"), "Május", "május"));
        using var reuse = Create(existing, [], Options());
        await reuse.EnsureMonth(2026, 5);

        Assert.Equal("számlák2026.xlsx", Assert.Single(existing.Files.Keys));
        using var reused = SpreadsheetDocument.Open(new MemoryStream(existing.Files["számlák2026.xlsx"]), false);
        var reusedNames = reused.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value).ToList();
        Assert.Equal(["Paraméterek", "május"], reusedNames);
        Assert.Contains(existing.Calls, call => call.Url.EndsWith("/checkout", StringComparison.Ordinal));
        Assert.All(existing.Calls.Where(call => call.Url.EndsWith("/checkout", StringComparison.Ordinal)), call => Assert.Equal(0, call.BodyLength));
        Assert.Contains(existing.Calls, call => call.Url.EndsWith("/checkin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_month_and_missing_parameters_are_created_from_the_previous_file()
    {
        var handler = new GraphDouble();
        handler.Add("SZÁMLÁK2026.xlsx", WorkbookFile.Create(2026, 1, "kept", "7,5,1"));
        handler.Add("SZÁMLÁK2025.xlsx", Parameters(" InsDate ", "  2024-06-01T00:00:00Z  ", "Előértesítési napok", "  9,5  "));
        using var workbook = Create(handler, [], Options());

        await workbook.EnsureMonth(2026, 5);

        using var document = SpreadsheetDocument.Open(new MemoryStream(handler.Files["SZÁMLÁK2026.xlsx"]), false);
        var names = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value).ToList();
        Assert.Equal(["Paraméterek", "Január", "Május"], names);
        Assert.DoesNotContain("Június", names);
        Assert.Equal("kept", Text(document.WorkbookPart, "Paraméterek", "B1"));
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains(handler.IdOf("SZÁMLÁK2025.xlsx"), StringComparison.Ordinal) && call.Url.Contains("/checkout", StringComparison.Ordinal));

        var missing = new GraphDouble();
        missing.Add("SZÁMLÁK2026.xlsx", ExistingMonth("megmarad"));
        missing.Add("SZÁMLÁK2025.xlsx", Parameters(" InsDate ", "  2024-06-01T00:00:00Z  ", "Előértesítési napok", "  9,5  ", "InsDate", "later"));
        using var fill = Create(missing, [], Options());
        await fill.EnsureMonth(2026, 5);

        using var filled = SpreadsheetDocument.Open(new MemoryStream(missing.Files["SZÁMLÁK2026.xlsx"]), false);
        Assert.Equal("2024-06-01T00:00:00Z", Text(filled.WorkbookPart!, "Paraméterek", "B1"));
        Assert.Equal("9,5", Text(filled.WorkbookPart, "Paraméterek", "B2"));
        Assert.Equal("megmarad", Text(filled.WorkbookPart, "Május", "A2"));
        Assert.DoesNotContain(missing.Calls, call => call.Url.Contains(missing.IdOf("SZÁMLÁK2025.xlsx"), StringComparison.Ordinal) && call.Url.Contains("/checkout", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Previous_file_outside_the_window_is_read_without_checkout_and_is_not_created()
    {
        var handler = new GraphDouble();
        handler.Add("SZÁMLÁK2024.xlsx", Parameters("InsDate", "  2024-06-01T00:00:00Z  ", "Előértesítési napok", "9,5"));
        using var workbook = Create(handler, [], Options());

        await workbook.EnsureMonth(2025, 1);

        using var document = SpreadsheetDocument.Open(new MemoryStream(handler.Files["SZÁMLÁK2025.xlsx"]), false);
        Assert.Equal("2024-06-01T00:00:00Z", Text(document.WorkbookPart!, "Paraméterek", "B1"));
        Assert.Equal("9,5", Text(document.WorkbookPart, "Paraméterek", "B2"));
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("/checkout", StringComparison.Ordinal) && call.Url.Contains(handler.IdOf("SZÁMLÁK2024.xlsx"), StringComparison.Ordinal));
        Assert.Contains(handler.Files, file => file.Key == "SZÁMLÁK2024.xlsx");
    }

    [Fact]
    public async Task Taken_name_continues_on_the_existing_file_without_replace_or_rename()
    {
        var handler = new GraphDouble { ConflictOnCreate = true };
        handler.Add("SZÁMLÁK2026.xlsx", WorkbookFile.Create(2026, 1, "kept", "7,5,1"));
        handler.Hide("SZÁMLÁK2026.xlsx");
        using var workbook = Create(handler, [], Options());

        await workbook.EnsureMonth(2026, 5);

        Assert.Equal(1, handler.Calls.Count(call => call.Url.Contains("conflictBehavior=fail", StringComparison.Ordinal)));
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("conflictBehavior=replace", StringComparison.Ordinal) || call.Url.Contains("conflictBehavior=rename", StringComparison.Ordinal));
        Assert.Contains(handler.Calls, call => call.Url.EndsWith("/checkout", StringComparison.Ordinal));
        using var document = SpreadsheetDocument.Open(new MemoryStream(handler.Files["SZÁMLÁK2026.xlsx"]), false);
        var names = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value).ToList();
        Assert.Contains("Január", names);
        Assert.Contains("Május", names);
    }

    [Fact]
    public async Task Failed_checkout_is_retried_then_stops_without_download_checkin_or_insdate_change()
    {
        var delays = new List<TimeSpan>();
        var handler = new GraphDouble { CheckoutStatus = HttpStatusCode.Conflict };
        var original = WorkbookFile.Create(2026, 5, "kept-ins", "7,5,1");
        handler.Add("SZÁMLÁK2026.xlsx", original);
        using var workbook = Create(handler, delays, Options());

        var error = await Assert.ThrowsAsync<ExcelWorkbookException>(() => workbook.EnsureMonth(2026, 5));

        Assert.Equal(4, handler.Calls.Count(call => call.Url.EndsWith("/checkout", StringComparison.Ordinal)));
        Assert.Equal(3, delays.Count);
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("/content", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, call => call.Url.EndsWith("/checkin", StringComparison.Ordinal));
        Assert.Equal(original, handler.Files["SZÁMLÁK2026.xlsx"]);
        Assert.Contains("SZÁMLÁK2026.xlsx", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("kept-ins", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-1", error.Message, StringComparison.Ordinal);
        Assert.Empty(typeof(ExcelWorkbookException).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));

        await Assert.ThrowsAsync<ExcelWorkbookException>(() => workbook.EnsureMonth(2026, 5));
        Assert.Equal(8, handler.Calls.Count(call => call.Url.EndsWith("/checkout", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Checkout_network_error_retries_in_its_own_loop()
    {
        var delays = new List<TimeSpan>();
        var handler = new GraphDouble { CheckoutThrows = 1 };
        handler.Add("SZÁMLÁK2026.xlsx", WorkbookFile.Create(2026, 5, "kept", "7,5,1"));
        using var workbook = Create(handler, delays, Options());

        await workbook.EnsureMonth(2026, 5);

        Assert.Equal(2, handler.Calls.Count(call => call.Url.EndsWith("/checkout", StringComparison.Ordinal)));
        Assert.Equal(1, delays.Count);
        Assert.Contains(handler.Calls, call => call.Url.EndsWith("/checkin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_upload_does_not_check_in_or_change_the_file()
    {
        var handler = new GraphDouble { FailUpload = true };
        var original = WorkbookFile.Create(2026, 1, "kept-ins", "7,5,1");
        handler.Add("SZÁMLÁK2026.xlsx", original);
        using var workbook = Create(handler, [], Options());

        var error = await Assert.ThrowsAsync<ExcelWorkbookException>(() => workbook.EnsureMonth(2026, 5));

        Assert.Contains(handler.Calls, call => call.Url.EndsWith("/checkout", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, call => call.Url.EndsWith("/checkin", StringComparison.Ordinal));
        Assert.Equal(original, handler.Files["SZÁMLÁK2026.xlsx"]);
        Assert.DoesNotContain("kept-ins", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Previous_file_download_failure_does_not_write_defaults()
    {
        var handler = new GraphDouble { FailDownloadsContaining = "SZÁMLÁK2024.xlsx" };
        handler.Add("SZÁMLÁK2024.xlsx", Parameters("InsDate", "2024-06-01T00:00:00Z", "Előértesítési napok", "9,5"));
        using var workbook = Create(handler, [], Options());

        await Assert.ThrowsAsync<ExcelWorkbookException>(() => workbook.EnsureMonth(2025, 1));

        Assert.DoesNotContain(handler.Files.Keys, name => name.Contains("2025", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, call => call.Method == "PUT");
        Assert.DoesNotContain(handler.Calls, call => call.Url.Contains("/checkout", StringComparison.Ordinal));
    }

    [Fact]
    public void Excel_source_does_not_send_slack_or_reference_nav()
    {
        foreach (var file in Directory.GetFiles(FindRepoFile(Path.Combine("src", "OsaPro.Excel")), "*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("Slack", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReservationDeferred", source, StringComparison.Ordinal);
            Assert.DoesNotContain("OsaPro.NAV", source, StringComparison.Ordinal);
            Assert.DoesNotContain("queryInvoice", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Worker_boundary_stays_outside_excel_and_nav()
    {
        var handler = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "FunctionHandlers.cs")));
        var workerProject = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "OsaPro.Worker.csproj")));
        Assert.DoesNotContain("EnsureMonth", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("TimerTrigger", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpTrigger", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("OsaPro.Excel", workerProject, StringComparison.Ordinal);

        foreach (var file in Directory.GetFiles(FindRepoFile(Path.Combine("src", "OsaPro.Common")), "*.cs"))
        {
            Assert.DoesNotContain("OsaPro.Excel", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    private static ExcelWorkbook Create(GraphDouble handler, List<TimeSpan> delays, ExcelOptions options)
    {
        return new ExcelWorkbook(
            options,
            handler,
            () => June2026,
            (span, _) =>
            {
                delays.Add(span);
                return Task.CompletedTask;
            });
    }

    private static ExcelOptions Options()
    {
        return new ExcelOptions
        {
            SiteHost = "contoso.sharepoint.com",
            SitePath = "/sites/OsaPro",
            FolderPath = "Szamlak",
            RetryCount = 3,
            RetryDelaySeconds = 5,
            TenantId = "tenant-1",
            ClientId = "client-1",
            ClientSecret = "secret-1"
        };
    }

    private static byte[] Rename(byte[] source, string from, string to)
    {
        using var stream = new MemoryStream();
        stream.Write(source);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var sheet = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == from);
            sheet.Name = to;
            document.WorkbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static byte[] ExistingMonth(string text)
    {
        using var stream = new MemoryStream();
        stream.Write(WorkbookFile.Create(2026, 5, "x", "y"));
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart!;
            var sheets = workbookPart.Workbook.Sheets!;
            var parameters = sheets.Elements<Sheet>().Single(item => item.Name == "Paraméterek");
            var parameterPart = workbookPart.GetPartById(parameters.Id!);
            parameters.Remove();
            workbookPart.DeletePart(parameterPart);
            var month = sheets.Elements<Sheet>().Single();
            var part = (WorksheetPart)workbookPart.GetPartById(month.Id!);
            var row = new Row { RowIndex = 2 };
            row.Append(TextCell("A2", text));
            part.Worksheet.GetFirstChild<SheetData>()!.Append(row);
            part.Worksheet.Save();
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static byte[] Parameters(string firstKey, string firstValue, string secondKey, string secondValue, string? thirdKey = null, string? thirdValue = null)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var part = workbookPart.AddNewPart<WorksheetPart>();
            var row1 = new Row { RowIndex = 1 };
            row1.Append(TextCell("A1", firstKey), TextCell("B1", firstValue));
            var row2 = new Row { RowIndex = 2 };
            row2.Append(TextCell("A2", secondKey), TextCell("B2", secondValue));
            var data = new SheetData(row1, row2);
            if (thirdKey is not null && thirdValue is not null)
            {
                var row3 = new Row { RowIndex = 3 };
                row3.Append(TextCell("A3", thirdKey), TextCell("B3", thirdValue));
                data.Append(row3);
            }

            part.Worksheet = new Worksheet(data);
            part.Worksheet.Save();
            sheets.Append(new Sheet { Name = "Paraméterek", SheetId = 1, Id = workbookPart.GetIdOfPart(part) });
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Cell TextCell(string reference, string text)
    {
        return new Cell
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        };
    }

    private static string Text(WorkbookPart workbook, string sheetName, string reference)
    {
        var sheet = workbook.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == sheetName);
        var part = (WorksheetPart)workbook.GetPartById(sheet.Id!);
        var cell = part.Worksheet.Descendants<Cell>().Single(item => item.CellReference == reference);
        return cell.InlineString!.Text!.Text;
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"A fájl nem található: {relative}");
    }

    private sealed class GraphDouble : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _files = [];
        private readonly Dictionary<string, string> _ids = [];
        private readonly HashSet<string> _hidden = [];
        private int _nextId = 1;

        public List<Call> Calls { get; } = [];

        public IReadOnlyDictionary<string, byte[]> Files => _files;

        public int TokenFailures { get; init; }

        public int TokenCalls { get; private set; }

        public string TokenBody { get; private set; } = "";

        public bool FolderExists { get; init; } = true;

        public bool SplitPages { get; init; }

        public bool ConflictOnCreate { get; init; }

        public bool FailUpload { get; init; }

        public int DownloadFailures { get; set; }

        public int CheckoutThrows { get; set; }

        public string? FailDownloadsContaining { get; init; }

        public HttpStatusCode? SiteStatus { get; init; }

        public HttpStatusCode CheckoutStatus { get; init; } = HttpStatusCode.NoContent;

        public void Add(string name, byte[] bytes)
        {
            _files[name] = bytes;
            _ids[name] = "item-" + _nextId++;
        }

        public void Hide(string name)
        {
            _hidden.Add(name);
        }

        public string IdOf(string name)
        {
            return _ids[name];
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
            var bytes = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Calls.Add(new Call(url, request.Method.Method, request.Headers.Authorization?.ToString(), bytes.Length));

            if (url.Contains("login.microsoftonline.com", StringComparison.Ordinal))
            {
                TokenCalls++;
                TokenBody = Encoding.UTF8.GetString(bytes);
                return TokenCalls <= TokenFailures
                    ? Status(HttpStatusCode.ServiceUnavailable)
                    : Json(HttpStatusCode.OK, """{"access_token":"token-1"}""");
            }

            if (url.StartsWith("https://download.test/", StringComparison.Ordinal))
            {
                var id = url["https://download.test/".Length..];
                var file = _ids.First(pair => pair.Value == id).Key;
                return Bytes(_files[file]);
            }

            if (url.Contains("/sites/contoso.sharepoint.com:", StringComparison.Ordinal))
            {
                return SiteStatus is null
                    ? Json(HttpStatusCode.OK, """{"id":"site-1"}""")
                    : Status(SiteStatus.Value);
            }

            if (url.Contains("/drive/root:/Szamlak", StringComparison.Ordinal) && !url.Contains(":/content", StringComparison.Ordinal))
            {
                return FolderExists
                    ? Json(HttpStatusCode.OK, """{"id":"folder-1"}""")
                    : Status(HttpStatusCode.NotFound);
            }

            if (url.Contains("/drive/items/folder-1/children", StringComparison.Ordinal))
            {
                var visible = _files.Keys.Where(name => !_hidden.Contains(name)).ToList();
                if (SplitPages && !url.Contains("page=2", StringComparison.Ordinal))
                {
                    return Json(HttpStatusCode.OK, """{"value":[],"@odata.nextLink":"https://graph.microsoft.com/v1.0/sites/site-1/drive/items/folder-1/children?page=2"}""");
                }

                var items = visible.Select(name => "{\"id\":" + JsonSerializer.Serialize(_ids[name]) + ",\"name\":" + JsonSerializer.Serialize(name) + "}");
                return Json(HttpStatusCode.OK, "{\"value\":[" + string.Join(',', items) + "]}");
            }

            if (url.Contains("conflictBehavior=fail", StringComparison.Ordinal) && request.Method == HttpMethod.Put)
            {
                if (ConflictOnCreate)
                {
                    _hidden.Clear();
                    return Status(HttpStatusCode.Conflict);
                }

                var created = FileName(url);
                _files[created] = bytes;
                _ids[created] = "item-" + _nextId++;
                return Status(HttpStatusCode.Created);
            }

            var itemId = ItemId(url);
            if (itemId is null)
            {
                return Status(HttpStatusCode.NotFound);
            }

            var name = _ids.First(pair => pair.Value == itemId).Key;
            if (url.EndsWith("/checkout", StringComparison.Ordinal))
            {
                if (CheckoutThrows > 0)
                {
                    CheckoutThrows--;
                    throw new HttpRequestException("A kapcsolat megszakadt.");
                }

                return Status(CheckoutStatus);
            }

            if (url.EndsWith("/checkin", StringComparison.Ordinal))
            {
                return Status(HttpStatusCode.NoContent);
            }

            if (url.EndsWith("/content", StringComparison.Ordinal) && request.Method == HttpMethod.Get)
            {
                if (FailDownloadsContaining is not null && name.Contains(FailDownloadsContaining, StringComparison.Ordinal))
                {
                    return Status(HttpStatusCode.InternalServerError);
                }

                if (DownloadFailures > 0)
                {
                    DownloadFailures--;
                    return Status(HttpStatusCode.InternalServerError);
                }

                return new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://download.test/" + itemId) }
                };
            }

            if (url.EndsWith("/content", StringComparison.Ordinal) && request.Method == HttpMethod.Put)
            {
                if (FailUpload)
                {
                    return Status(HttpStatusCode.BadRequest);
                }

                _files[name] = bytes;
                return Status(HttpStatusCode.OK);
            }

            return Status(HttpStatusCode.NotFound);
        }

        private static string FileName(string url)
        {
            const string marker = "/drive/root:/Szamlak/";
            var start = url.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = url.IndexOf(":/content", start, StringComparison.Ordinal);
            return url[start..end];
        }

        private string? ItemId(string url)
        {
            const string marker = "/drive/items/";
            var start = url.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0 || url.Contains("folder-1", StringComparison.Ordinal))
            {
                return null;
            }

            var rest = url[(start + marker.Length)..];
            var slash = rest.IndexOf('/');
            return slash < 0 ? rest : rest[..slash];
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage Bytes(byte[] body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body)
            };
        }

        private static HttpResponseMessage Status(HttpStatusCode status)
        {
            return new HttpResponseMessage(status);
        }
    }

    private sealed record Call(string Url, string Method, string? Authorization, int BodyLength);
}
