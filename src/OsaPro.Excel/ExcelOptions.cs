using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace OsaPro.Excel;

public sealed class ExcelOptions
{
    public string SiteHost { get; init; } = "";

    public string SitePath { get; init; } = "";

    public string FolderPath { get; init; } = "";

    public int RetryCount { get; init; } = 3;

    public int RetryDelaySeconds { get; init; } = 5;

    public string TenantId { get; init; } = "";

    public string ClientId { get; init; } = "";

    public string ClientSecret { get; init; } = "";

    public static ExcelOptions FromConfiguration(IConfiguration configuration)
    {
        return new ExcelOptions
        {
            SiteHost = configuration["Excel:SiteHost"] ?? "",
            SitePath = configuration["Excel:SitePath"] ?? "",
            FolderPath = configuration["Excel:FolderPath"] ?? "",
            RetryCount = int.Parse(configuration["Excel:RetryCount"] ?? "3", CultureInfo.InvariantCulture),
            RetryDelaySeconds = int.Parse(configuration["Excel:RetryDelaySeconds"] ?? "5", CultureInfo.InvariantCulture),
            TenantId = configuration["Secrets:ExcelTenantId"] ?? "",
            ClientId = configuration["Secrets:ExcelClientId"] ?? "",
            ClientSecret = configuration["Secrets:ExcelClientSecret"] ?? ""
        };
    }
}
