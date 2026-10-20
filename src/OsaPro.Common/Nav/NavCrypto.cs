using System.Security.Cryptography;
using System.Text;

namespace OsaPro.Common.Nav;

internal static class NavCrypto
{
    public static string PasswordHash(string password)
    {
        return UpperHex(SHA512.HashData(Encoding.UTF8.GetBytes(password)));
    }

    public static string RequestSignature(string requestId, DateTimeOffset timestamp, string signingKey)
    {
        var source = requestId + NavTimestamps.SignatureMask(timestamp) + signingKey;
        return UpperHex(SHA3_512.HashData(Encoding.UTF8.GetBytes(source)));
    }

    public static string RequestId()
    {
        return Guid.NewGuid().ToString("N")[..30];
    }

    private static string UpperHex(byte[] hash)
    {
        return Convert.ToHexString(hash);
    }
}
