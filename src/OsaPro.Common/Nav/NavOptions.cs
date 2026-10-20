using Microsoft.Extensions.Configuration;

namespace OsaPro.Common.Nav;

public sealed class NavSoftwareOptions
{
    public required string SoftwareId { get; init; }

    public required string SoftwareName { get; init; }

    public required string SoftwareOperation { get; init; }

    public required string SoftwareMainVersion { get; init; }

    public required string SoftwareDevName { get; init; }

    public required string SoftwareDevContact { get; init; }
}

public sealed class NavOptions
{
    public required string BaseUrl { get; init; }

    public int RetryCount { get; init; } = 3;

    public int RetryDelaySeconds { get; init; } = 5;

    public required NavSoftwareOptions Software { get; init; }

    public string Login { get; init; } = "";

    public string TaxNumber { get; init; } = "";

    public string Password { get; init; } = "";

    public string SigningKey { get; init; } = "";

    public static NavOptions FromConfiguration(IConfiguration configuration)
    {
        var software = configuration.GetSection("Nav:Software");
        return new NavOptions
        {
            BaseUrl = configuration["Nav:BaseUrl"] ?? "",
            RetryCount = int.Parse(configuration["Nav:RetryCount"] ?? "3", System.Globalization.CultureInfo.InvariantCulture),
            RetryDelaySeconds = int.Parse(configuration["Nav:RetryDelaySeconds"] ?? "5", System.Globalization.CultureInfo.InvariantCulture),
            Software = new NavSoftwareOptions
            {
                SoftwareId = software["SoftwareId"] ?? "",
                SoftwareName = software["SoftwareName"] ?? "",
                SoftwareOperation = software["SoftwareOperation"] ?? "",
                SoftwareMainVersion = software["SoftwareMainVersion"] ?? "",
                SoftwareDevName = software["SoftwareDevName"] ?? "",
                SoftwareDevContact = software["SoftwareDevContact"] ?? ""
            },
            Login = configuration["Secrets:NavLogin"] ?? "",
            TaxNumber = configuration["Secrets:NavTaxNumber"] ?? "",
            Password = configuration["Secrets:NavPassword"] ?? "",
            SigningKey = configuration["Secrets:NavSigningKey"] ?? ""
        };
    }
}
