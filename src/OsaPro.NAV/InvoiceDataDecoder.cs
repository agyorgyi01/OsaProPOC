using System.IO.Compression;
using System.Text;
using System.Xml.Serialization;
using InvoiceDocument = OsaPro.NAV.InvoiceData.InvoiceData;

namespace OsaPro.NAV;

internal static class InvoiceDataDecoder
{
    private static readonly XmlSerializer Serializer = new(typeof(InvoiceDocument));

    public static InvoiceDocument Read(string base64, bool compressed)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new NavQueryException("A bizonylat Base64 tartalma nem olvasható.", exception);
        }

        if (compressed)
        {
            try
            {
                using var input = new MemoryStream(bytes);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                bytes = output.ToArray();
            }
            catch (InvalidDataException exception)
            {
                throw new NavQueryException("A bizonylat tömörítése nem bontható.", exception);
            }
        }

        try
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
            if (Serializer.Deserialize(reader) is not InvoiceDocument invoice)
            {
                throw new NavQueryException("A bizonylat XML üres.");
            }

            return invoice;
        }
        catch (InvalidOperationException exception)
        {
            throw new NavQueryException("A bizonylat XML nem olvasható.", exception);
        }
    }
}
