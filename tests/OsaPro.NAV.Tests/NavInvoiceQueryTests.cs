using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using OsaPro.Common;
using OsaPro.NAV;
using OsaPro.Worker;
using InvoiceDocument = OsaPro.NAV.InvoiceData.InvoiceData;

namespace OsaPro.NAV.Tests;

public class NavInvoiceQueryTests
{
    private static readonly DateTimeOffset ClockTime = new(2026, 10, 20, 5, 29, 0, TimeSpan.Zero);

    [Fact]
    public void Versioned_nav_settings_keep_secrets_empty()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(FindVersionedAppSettings()).Build();
        var options = NavOptions.FromConfiguration(configuration);
        var text = File.ReadAllText(FindVersionedAppSettings());

        Assert.Contains(
            options.BaseUrl,
            new[]
            {
                "https://api-test.onlineszamla.nav.gov.hu",
                "https://api.onlineszamla.nav.gov.hu"
            });
        Assert.Equal(3, options.RetryCount);
        Assert.Equal(5, options.RetryDelaySeconds);
        Assert.Equal("HU-OSP-00000000001", options.Software.SoftwareId);
        Assert.Equal("OsaPro", options.Software.SoftwareName);
        Assert.Equal("ONLINE_SERVICE", options.Software.SoftwareOperation);
        Assert.Equal("1.0", options.Software.SoftwareMainVersion);
        Assert.Equal("OSP", options.Software.SoftwareDevName);
        Assert.Equal("info@nexande.ocm", options.Software.SoftwareDevContact);
        Assert.Equal("", options.Login);
        Assert.Equal("", options.TaxNumber);
        Assert.Equal("", options.Password);
        Assert.Equal("", options.SigningKey);
        Assert.Equal("", configuration["Secrets:NavLogin"]);
        Assert.Equal("", configuration["Secrets:NavTaxNumber"]);
        Assert.Equal("", configuration["Secrets:NavPassword"]);
        Assert.Equal("", configuration["Secrets:NavSigningKey"]);
        Assert.DoesNotContain("jelszo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alairkulcs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_contains_hashes_software_and_no_raw_secret()
    {
        var options = RequireOptions();
        var handler = OkDigest();
        var delays = new List<TimeSpan>();
        using var query = Create(handler, delays, () => "TESTREQ0001", options: options);

        await query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), ClockTime, CancellationToken.None);

        var body = handler.Bodies.Single();
        var xml = XDocument.Parse(body);
        var passwordHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(options.Password)));
        var signature = Convert.ToHexString(SHA3_512.HashData(Encoding.UTF8.GetBytes("TESTREQ0001" + "20261020052900" + options.SigningKey)));
        Assert.Equal(passwordHash, Value(xml, "passwordHash"));
        Assert.Equal("SHA-512", Attribute(xml, "passwordHash", "cryptoType"));
        Assert.Equal(signature, Value(xml, "requestSignature"));
        Assert.Equal("SHA3-512", Attribute(xml, "requestSignature", "cryptoType"));
        Assert.Equal("3.0", Value(xml, "requestVersion"));
        Assert.Equal("INBOUND", Value(xml, "invoiceDirection"));
        Assert.Equal(options.Login, Value(xml, "login"));
        Assert.Equal(options.TaxNumber, Value(xml, "taxNumber"));
        Assert.Equal(options.Software.SoftwareId, Value(xml, "softwareId"));
        Assert.Equal(options.Software.SoftwareName, Value(xml, "softwareName"));
        Assert.Equal(options.Software.SoftwareOperation, Value(xml, "softwareOperation"));
        Assert.Equal(options.Software.SoftwareMainVersion, Value(xml, "softwareMainVersion"));
        Assert.Equal(options.Software.SoftwareDevName, Value(xml, "softwareDevName"));
        Assert.Equal(options.Software.SoftwareDevContact, Value(xml, "softwareDevContact"));
        Assert.DoesNotContain(options.Password, body, StringComparison.Ordinal);
        Assert.DoesNotContain(options.SigningKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenExchange", handler.Bodies.Single(), StringComparison.OrdinalIgnoreCase);
        var request = handler.Requests.Single();
        Assert.StartsWith(options.BaseUrl.TrimEnd('/'), request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/invoiceService/v3/queryInvoiceDigest", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("application/xml", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", request.Content.Headers.ContentType!.CharSet);
        Assert.Contains(request.Headers.Accept, header => header.MediaType == "application/xml");
        Assert.Empty(delays);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("429")]
    [InlineData("500")]
    public async Task Transient_failure_retries_three_times_without_sleeping(string kind)
    {
        var options = RequireOptions();
        var calls = 0;
        var handler = new ScriptedHandler((call, _) =>
        {
            calls++;
            if (call < options.RetryCount)
            {
                return kind switch
                {
                    "network" => throw new HttpRequestException("hálózat"),
                    "timeout" => throw new TaskCanceledException("időtúllépés"),
                    "429" => Status(HttpStatusCode.TooManyRequests),
                    "500" => Status(HttpStatusCode.InternalServerError),
                    _ => throw new InvalidOperationException(kind)
                };
            }

            return Xml(DigestResponse(1, 0));
        });
        var delays = new List<TimeSpan>();
        var watch = Stopwatch.StartNew();
        using var query = Create(handler, delays, () => "TESTREQ0001", options: options);

        var items = await query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), ClockTime, CancellationToken.None);

        watch.Stop();
        Assert.Empty(items);
        Assert.Equal(options.RetryCount + 1, calls);
        Assert.Equal(options.RetryCount, delays.Count);
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromSeconds(options.RetryDelaySeconds), delay));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.Single(handler.Bodies.Distinct());
    }

    [Theory]
    [InlineData("error")]
    [InlineData("400")]
    public async Task Business_failure_is_not_retried(string kind)
    {
        var handler = new ScriptedHandler((_, _) => kind == "400"
            ? Status(HttpStatusCode.BadRequest)
            : Xml("""
                <QueryInvoiceDigestResponse xmlns="http://schemas.nav.gov.hu/OSA/3.0/api" xmlns:common="http://schemas.nav.gov.hu/NTCA/1.0/common">
                  <common:result><common:funcCode>ERROR</common:funcCode></common:result>
                </QueryInvoiceDigestResponse>
                """));
        var delays = new List<TimeSpan>();
        using var query = Create(handler, delays, () => "TESTREQ0001");

        await Assert.ThrowsAsync<NavQueryException>(() => query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), ClockTime, CancellationToken.None));

        Assert.Single(handler.Bodies);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Digest_uses_utc_day_bounds_and_clamps_to_the_start_date()
    {
        var open = OkDigest();
        using var openQuery = Create(open, [], () => "TESTREQ0001");
        await openQuery.QueryInvoiceDigest(new DateOnly(2026, 1, 1), new DateTimeOffset(2026, 10, 20, 15, 4, 0, TimeSpan.Zero), CancellationToken.None);
        var openXml = XDocument.Parse(open.Bodies.Single());
        Assert.Equal("2026-10-20T00:00:00.000Z", Value(openXml, "dateTimeFrom"));
        Assert.Equal("2026-10-21T00:00:00.000Z", Value(openXml, "dateTimeTo"));

        var clamped = OkDigest();
        using var clampedQuery = Create(clamped, [], () => "TESTREQ0001");
        await clampedQuery.QueryInvoiceDigest(new DateOnly(2026, 10, 5), new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), CancellationToken.None);
        var clampedXml = XDocument.Parse(clamped.Bodies.Single());
        Assert.Equal("2026-10-05T00:00:00.000Z", Value(clampedXml, "dateTimeFrom"));
        Assert.Equal("2026-10-21T00:00:00.000Z", Value(clampedXml, "dateTimeTo"));
    }

    [Fact]
    public async Task Long_window_is_split_into_slices_of_at_most_35_days()
    {
        var handler = OkDigest();
        using var query = Create(handler, [], () => "TESTREQ0001", new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));

        await query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), CancellationToken.None);

        var bounds = handler.Bodies.Select(body =>
        {
            var xml = XDocument.Parse(body);
            return (From: Value(xml, "dateTimeFrom"), To: Value(xml, "dateTimeTo"));
        }).ToArray();
        Assert.Equal(
            [
                ("2026-01-01T00:00:00.000Z", "2026-02-05T00:00:00.000Z"),
                ("2026-02-05T00:00:00.001Z", "2026-03-12T00:00:00.001Z"),
                ("2026-03-12T00:00:00.002Z", "2026-03-21T00:00:00.000Z")
            ],
            bounds);
        Assert.All(handler.Requests, request => Assert.Contains("/queryInvoiceDigest", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsoluteUri.Contains("queryInvoiceData", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Digest_returns_every_page_and_keeps_operations()
    {
        var handler = new ScriptedHandler((_, body) =>
        {
            var page = Value(XDocument.Parse(body), "page");
            return page switch
            {
                "1" => Xml(DigestResponse(1, 2, Item("CREATE", "SZ-1"))),
                "2" => Xml(DigestResponse(2, 2, Item("MODIFY", "SZ-2"), Item("STORNO", "SZ-3"))),
                _ => throw new InvalidOperationException(page)
            };
        });
        using var query = Create(handler, [], () => "TESTREQ0001");

        var items = await query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), ClockTime, CancellationToken.None);

        Assert.Equal(["CREATE", "MODIFY", "STORNO"], items.Select(item => item.InvoiceOperation).ToArray());
        Assert.Equal(["SZ-1", "SZ-2", "SZ-3"], items.Select(item => item.InvoiceNumber).ToArray());
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsoluteUri.Contains("queryInvoiceData", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Empty_digest_is_success()
    {
        var untouched = new ScriptedHandler((_, _) => throw new InvalidOperationException("nem kell hívás"));
        using var future = Create(untouched, [], () => "TESTREQ0001");
        var none = await future.QueryInvoiceDigest(new DateOnly(2026, 10, 22), ClockTime, CancellationToken.None);
        Assert.Empty(none);
        Assert.Empty(untouched.Bodies);

        var handler = OkDigest();
        using var query = Create(handler, [], () => "TESTREQ0001");
        var items = await query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), ClockTime, CancellationToken.None);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Failed_later_slice_does_not_return_a_partial_list()
    {
        var options = RequireOptions();
        var handler = new ScriptedHandler((call, _) => call == 0
            ? Xml(DigestResponse(1, 1, Item("CREATE", "SZ-1")))
            : Status(HttpStatusCode.InternalServerError));
        using var query = Create(handler, [], () => "TESTREQ0001", new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero), options);

        await Assert.ThrowsAsync<NavQueryException>(() =>
            query.QueryInvoiceDigest(new DateOnly(2026, 1, 1), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), CancellationToken.None));

        Assert.Equal(2 + options.RetryCount, handler.Bodies.Count);
    }

    [Fact]
    public async Task Invoice_query_returns_the_document_and_sends_batch_index_only_when_requested()
    {
        var plain = InvoiceXml("12345678-12345678-12345678");
        var compressedBytes = Gzip(Encoding.UTF8.GetBytes(InvoiceXml("11111111-22222222-33333333")));
        var missing = InvoiceXml(null);
        var handler = new ScriptedHandler((_, body) =>
        {
            var number = Value(XDocument.Parse(body), "invoiceNumber");
            return number switch
            {
                "SZ-1" => Xml(InvoiceResponse(Convert.ToBase64String(Encoding.UTF8.GetBytes(plain)), false)),
                "SZ-2" => Xml(InvoiceResponse(Convert.ToBase64String(compressedBytes), true)),
                "SZ-3" => Xml(InvoiceResponse(Convert.ToBase64String(Encoding.UTF8.GetBytes(missing)), false)),
                _ => throw new InvalidOperationException(number)
            };
        });
        using var query = Create(handler, [], () => "TESTREQ0001");

        var first = await query.QueryInvoiceData("SZ-1", "12345678", batchIndex: 2);
        var second = await query.QueryInvoiceData("SZ-2", "12345678");
        var third = await query.QueryInvoiceData("SZ-3", "12345678");

        Assert.Equal("12345678-12345678-12345678", BankAccount(first));
        Assert.Equal("11111111-22222222-33333333", BankAccount(second));
        Assert.True(string.IsNullOrEmpty(BankAccount(third)));
        Assert.Equal("2", Value(XDocument.Parse(handler.Bodies[0]), "batchIndex"));
        Assert.Equal("INBOUND", Value(XDocument.Parse(handler.Bodies[0]), "invoiceDirection"));
        Assert.Equal("12345678", Value(XDocument.Parse(handler.Bodies[0]), "supplierTaxNumber"));
        Assert.Null(Element(XDocument.Parse(handler.Bodies[1]), "batchIndex"));
        Assert.Contains("/invoiceService/v3/queryInvoiceData", handler.Requests[0].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenExchange", string.Join('\n', handler.Bodies), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Common_namespace_matches_the_vendored_schema()
    {
        var schema = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.NAV", "Schemas", "common.xsd")));
        Assert.Contains("targetNamespace=\"http://schemas.nav.gov.hu/NTCA/1.0/common\"", schema, StringComparison.Ordinal);
        var data = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.NAV", "Schemas", "invoiceData.xsd")));
        Assert.Contains("targetNamespace=\"http://schemas.nav.gov.hu/OSA/3.0/data\"", data, StringComparison.Ordinal);
    }

    private static string BankAccount(InvoiceDocument invoice)
    {
        return invoice.InvoiceMain?.Invoice?.InvoiceHead?.SupplierInfo?.SupplierBankAccountNumber ?? "";
    }

    private static InboundInvoiceQuery Create(
        ScriptedHandler handler,
        List<TimeSpan> delays,
        Func<string> requestIds,
        DateTimeOffset? clock = null,
        NavOptions? options = null)
    {
        var now = clock ?? ClockTime;
        return new InboundInvoiceQuery(
            options ?? RequireOptions(),
            handler,
            () => now,
            (span, _) =>
            {
                delays.Add(span);
                return Task.CompletedTask;
            },
            requestIds);
    }

    private static NavOptions RequireOptions()
    {
        var contentRoot = Path.GetDirectoryName(FindVersionedAppSettings())
            ?? throw new InvalidOperationException("A verziókezelt appsettings.json könyvtára nem olvasható.");
        var builder = new ConfigurationBuilder();
        SecretConfiguration.AddSources(
            builder,
            contentRoot,
            includeUserSecrets: true,
            userSecretsId: WorkerComposition.UserSecretsId);
        var options = NavOptions.FromConfiguration(builder.Build());
        var missing = new (string Name, string Value)[]
        {
            ("NavLogin", options.Login),
            ("NavTaxNumber", options.TaxNumber),
            ("NavPassword", options.Password),
            ("NavSigningKey", options.SigningKey)
        }
        .Where(item => string.IsNullOrWhiteSpace(item.Value))
        .Select(item => item.Name)
        .ToArray();
        if (missing.Length > 0)
        {
            Assert.Fail($"A NAV titkos kulcsok üresek ({string.Join(", ", missing)}). A teszt nem hívja a NAV-ot.");
        }

        return options;
    }

    private static ScriptedHandler OkDigest()
    {
        return new ScriptedHandler((_, _) => Xml(DigestResponse(1, 0)));
    }

    private static HttpResponseMessage Xml(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/xml")
        };
    }

    private static HttpResponseMessage Status(HttpStatusCode status)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent("", Encoding.UTF8, "application/xml")
        };
    }

    private static string DigestResponse(int current, int available, params string[] items)
    {
        return $"""
            <QueryInvoiceDigestResponse xmlns="http://schemas.nav.gov.hu/OSA/3.0/api" xmlns:common="http://schemas.nav.gov.hu/NTCA/1.0/common">
              <common:result><common:funcCode>OK</common:funcCode></common:result>
              <invoiceDigestResult>
                <currentPage>{current}</currentPage>
                <availablePage>{available}</availablePage>
                {string.Join("", items)}
              </invoiceDigestResult>
            </QueryInvoiceDigestResponse>
            """;
    }

    private static string Item(string operation, string number)
    {
        return $"""
            <invoiceDigest>
              <invoiceNumber>{number}</invoiceNumber>
              <invoiceOperation>{operation}</invoiceOperation>
              <invoiceCategory>NORMAL</invoiceCategory>
              <invoiceIssueDate>2026-10-01</invoiceIssueDate>
              <supplierTaxNumber>12345678</supplierTaxNumber>
              <supplierName>Partner</supplierName>
              <insDate>2026-10-01T12:00:00.000Z</insDate>
            </invoiceDigest>
            """;
    }

    private static string InvoiceResponse(string base64, bool compressed)
    {
        return $"""
            <QueryInvoiceDataResponse xmlns="http://schemas.nav.gov.hu/OSA/3.0/api" xmlns:common="http://schemas.nav.gov.hu/NTCA/1.0/common">
              <common:result><common:funcCode>OK</common:funcCode></common:result>
              <invoiceDataResult>
                <invoiceData>{base64}</invoiceData>
                <auditData><insDate>2026-10-01T12:00:00.000Z</insDate></auditData>
                <compressedContentIndicator>{(compressed ? "true" : "false")}</compressedContentIndicator>
              </invoiceDataResult>
            </QueryInvoiceDataResponse>
            """;
    }

    private static string InvoiceXml(string? bankAccount)
    {
        var account = bankAccount is null ? "" : $"<supplierBankAccountNumber>{bankAccount}</supplierBankAccountNumber>";
        return $"""
            <InvoiceData xmlns="http://schemas.nav.gov.hu/OSA/3.0/data">
              <invoiceNumber>SZ-1</invoiceNumber>
              <invoiceIssueDate>2026-10-01</invoiceIssueDate>
              <completenessIndicator>false</completenessIndicator>
              <invoiceMain>
                <invoice>
                  <invoiceHead>
                    <supplierInfo>
                      <supplierName>Partner</supplierName>
                      {account}
                    </supplierInfo>
                  </invoiceHead>
                </invoice>
              </invoiceMain>
            </InvoiceData>
            """;
    }

    private static byte[] Gzip(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(content);
        }

        return output.ToArray();
    }

    private static string Value(XDocument document, string localName)
    {
        return Element(document, localName)?.Value
            ?? throw new InvalidOperationException(localName);
    }

    private static string? Attribute(XDocument document, string localName, string attribute)
    {
        return Element(document, localName)?.Attribute(attribute)?.Value;
    }

    private static XElement? Element(XDocument document, string localName)
    {
        return document.Descendants().FirstOrDefault(element => element.Name.LocalName == localName);
    }

    private static string FindVersionedAppSettings()
    {
        return FindRepoFile(Path.Combine("src", "OsaPro.Worker", "appsettings.json"));
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<int, string, HttpResponseMessage> _next;
        private int _calls;

        public ScriptedHandler(Func<int, string, HttpResponseMessage> next)
        {
            _next = next;
        }

        public List<string> Bodies { get; } = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            Requests.Add(request);
            return _next(_calls++, body);
        }
    }
}
