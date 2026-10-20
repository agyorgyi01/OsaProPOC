using System.Text;
using System.Xml.Linq;

namespace OsaPro.Common.Nav;

internal static class NavNamespaces
{
    public const string Api = "http://schemas.nav.gov.hu/OSA/3.0/api";

    public const string Common = "http://schemas.nav.gov.hu/NTCA/1.0/common";
}

internal static class NavRequestEnvelope
{
    private static readonly XNamespace Api = NavNamespaces.Api;

    private static readonly XNamespace Common = NavNamespaces.Common;

    public static string Digest(
        NavOptions options,
        string requestId,
        DateTimeOffset timestamp,
        int page,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var root = Root("QueryInvoiceDigestRequest", options, requestId, timestamp);
        root.Add(
            new XElement(Api + "page", page.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(Api + "invoiceDirection", "INBOUND"),
            new XElement(
                Api + "invoiceQueryParams",
                new XElement(
                    Api + "mandatoryQueryParams",
                    new XElement(
                        Api + "insDate",
                        new XElement(Api + "dateTimeFrom", NavTimestamps.Format(from)),
                        new XElement(Api + "dateTimeTo", NavTimestamps.Format(to))))));
        return Document(root);
    }

    public static string Invoice(
        NavOptions options,
        string requestId,
        DateTimeOffset timestamp,
        string invoiceNumber,
        string supplierTaxNumber,
        int? batchIndex)
    {
        var query = new XElement(
            Api + "invoiceNumberQuery",
            new XElement(Api + "invoiceNumber", invoiceNumber),
            new XElement(Api + "invoiceDirection", "INBOUND"));
        if (batchIndex is int index)
        {
            query.Add(new XElement(Api + "batchIndex", index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        query.Add(new XElement(Api + "supplierTaxNumber", supplierTaxNumber));
        var root = Root("QueryInvoiceDataRequest", options, requestId, timestamp);
        root.Add(query);
        return Document(root);
    }

    private static XElement Root(string name, NavOptions options, string requestId, DateTimeOffset timestamp)
    {
        var software = options.Software;
        return new XElement(
            Api + name,
            new XAttribute(XNamespace.Xmlns + "common", Common.NamespaceName),
            new XElement(
                Common + "header",
                new XElement(Common + "requestId", requestId),
                new XElement(Common + "timestamp", NavTimestamps.Format(timestamp)),
                new XElement(Common + "requestVersion", "3.0"),
                new XElement(Common + "headerVersion", "1.0")),
            new XElement(
                Common + "user",
                new XElement(Common + "login", options.Login),
                new XElement(
                    Common + "passwordHash",
                    new XAttribute("cryptoType", "SHA-512"),
                    NavCrypto.PasswordHash(options.Password)),
                new XElement(Common + "taxNumber", options.TaxNumber),
                new XElement(
                    Common + "requestSignature",
                    new XAttribute("cryptoType", "SHA3-512"),
                    NavCrypto.RequestSignature(requestId, timestamp, options.SigningKey))),
            new XElement(
                Api + "software",
                new XElement(Api + "softwareId", software.SoftwareId),
                new XElement(Api + "softwareName", software.SoftwareName),
                new XElement(Api + "softwareOperation", software.SoftwareOperation),
                new XElement(Api + "softwareMainVersion", software.SoftwareMainVersion),
                new XElement(Api + "softwareDevName", software.SoftwareDevName),
                new XElement(Api + "softwareDevContact", software.SoftwareDevContact)));
    }

    private static string Document(XElement root)
    {
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var writer = new Utf8StringWriter();
        document.Save(writer);
        return writer.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
