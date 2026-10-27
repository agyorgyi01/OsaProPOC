using Microsoft.Extensions.Configuration;
using OsaPro.Common;
using OsaPro.Excel;
using OsaPro.Worker;

namespace OsaPro.Excel.Tests;

public class WorkbookRulesTests
{
    private static readonly DateTimeOffset June2026 = new(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void May_2026_uses_the_canonical_file_and_sheet_names()
    {
        Assert.Equal("SZÁMLÁK2026.xlsx", WorkbookRules.FileName(2026));
        Assert.Equal("Május", WorkbookRules.MonthName(5));
        Assert.Equal(16, WorkbookRules.Columns.Length);
        Assert.Equal("Partner neve", WorkbookRules.Columns[0]);
        Assert.Equal("Megjegyzés", WorkbookRules.Columns[15]);
    }

    [Fact]
    public void Names_match_case_and_edge_whitespace_but_keep_accents_and_inner_spaces()
    {
        Assert.Equal("május", WorkbookRules.FindMatch(["május"], "Május"));
        Assert.Equal(" Május ", WorkbookRules.FindMatch([" Május "], "Május"));
        Assert.Null(WorkbookRules.FindMatch(["Majus"], "Május"));

        Assert.Equal("számlák2026.xlsx", WorkbookRules.FindMatch(["számlák2026.xlsx"], WorkbookRules.FileName(2026)));
        Assert.Null(WorkbookRules.FindMatch(["SZAMLAK2026.xlsx"], WorkbookRules.FileName(2026)));

        Assert.Equal(" Partner neve ", WorkbookRules.FindMatch([" Partner neve "], "Partner neve"));
        Assert.Null(WorkbookRules.FindMatch(["Partner  neve"], "Partner neve"));
        Assert.Equal("bankszámlaszám", WorkbookRules.FindMatch(["bankszámlaszám"], "Bankszámlaszám"));
        Assert.Null(WorkbookRules.FindMatch(["Bankszamlaszam"], "Bankszámlaszám"));
        Assert.Equal(" InsDate ", WorkbookRules.FindMatch([" InsDate "], WorkbookRules.InsDateKey));
        Assert.Null(WorkbookRules.FindMatch(["Eloertesitesi napok"], WorkbookRules.NoticeDaysKey));
    }

    [Fact]
    public void Duplicate_header_keeps_the_first_column()
    {
        var headers = WorkbookRules.Columns.ToList();
        headers.Insert(4, "Bankszámlaszám");
        var inserts = WorkbookRules.PlanColumnInserts(headers);

        Assert.DoesNotContain(inserts, insert => insert.Name == "Bankszámlaszám");
    }

    [Fact]
    public void Missing_bank_account_is_inserted_before_the_next_canonical_header()
    {
        var between = new List<string> { "Partner neve", "Adószám", "Számlasorszám" };
        between.AddRange(WorkbookRules.Columns.Skip(4));
        var direct = WorkbookRules.PlanColumnInserts(between);
        Assert.Contains(direct, insert => insert.Name == "Bankszámlaszám" && insert.Index == 2);

        var foreign = new List<string> { "Partner neve", "Adószám", "Saját", "Számlasorszám" };
        foreign.AddRange(WorkbookRules.Columns.Skip(4));
        var shifted = WorkbookRules.PlanColumnInserts(foreign);
        Assert.Contains(shifted, insert => insert.Name == "Bankszámlaszám" && insert.Index == 3);
    }

    [Fact]
    public void Empty_header_inserts_all_columns_at_the_start()
    {
        var inserts = WorkbookRules.PlanColumnInserts(["Idegen"]);
        Assert.Equal(WorkbookRules.Columns, inserts.Select(insert => insert.Name).ToArray());
        Assert.Equal(Enumerable.Range(0, 16), inserts.Select(insert => insert.Index));
    }

    [Fact]
    public void Handled_years_follow_the_injected_budapest_clock()
    {
        Assert.True(WorkbookRules.IsHandledYear(2026, June2026));
        Assert.True(WorkbookRules.IsHandledYear(2025, June2026));
        Assert.False(WorkbookRules.IsHandledYear(2024, June2026));
        var yearTurn = new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero);
        Assert.Equal(2026, WorkbookRules.CurrentYear(yearTurn));
    }

    [Fact]
    public void Invalid_month_year_and_address_fail_before_a_file_is_created()
    {
        Assert.Throws<ExcelWorkbookException>(() => WorkbookRules.MonthName(0));
        Assert.Throws<ExcelWorkbookException>(() => WorkbookRules.RequireMonth(13));
        Assert.Throws<ExcelWorkbookException>(() => WorkbookRules.RequireHandledYear(2024, June2026));
        Assert.Throws<ExcelWorkbookException>(() => WorkbookRules.RequireAddress(new ExcelOptions()));
        Assert.Throws<ExcelWorkbookException>(() => WorkbookRules.RequireAddress(new ExcelOptions
        {
            SiteHost = " ",
            SitePath = "/sites/OsaPro",
            FolderPath = "Szamlak"
        }));
    }

    [Fact]
    public void Parameters_use_the_first_key_trim_the_value_and_fall_back_when_missing()
    {
        var swapped = WorkbookRules.ResolveParameters(
            [
                ("Előértesítési napok", "9,5"),
                ("InsDate", "  2024-06-01T00:00:00Z  ")
            ],
            2026);
        Assert.Equal("2024-06-01T00:00:00Z", swapped.InsDate);
        Assert.Equal("9,5", swapped.NoticeDays);

        var duplicate = WorkbookRules.ResolveParameters(
            [
                ("insdate", "first-value"),
                ("InsDate", "second-value"),
                ("Előértesítési napok", "   ")
            ],
            2026);
        Assert.Equal("first-value", duplicate.InsDate);
        Assert.Equal("7,5,1", duplicate.NoticeDays);

        var missing = WorkbookRules.ResolveParameters([], 2026);
        Assert.Equal("2025-12-31T00:00:00Z", missing.InsDate);
        Assert.Equal("7,5,1", missing.NoticeDays);

        var spacedKey = WorkbookRules.ResolveParameters([(" InsDate ", "kept")], 2026);
        Assert.Equal("kept", spacedKey.InsDate);
        var accentless = WorkbookRules.ResolveParameters([("Eloertesitesi napok", "1")], 2026);
        Assert.Equal("7,5,1", accentless.NoticeDays);
    }

    [Fact]
    public void Versioned_excel_settings_are_empty_secrets_and_independent_retry_values()
    {
        var contentRoot = Path.GetDirectoryName(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "appsettings.json")))!;
        var builder = new ConfigurationBuilder();
        SecretConfiguration.AddSources(builder, contentRoot, includeUserSecrets: false, userSecretsId: null);
        var configuration = builder.Build();
        var options = ExcelOptions.FromConfiguration(configuration);
        var json = File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "appsettings.json")));

        Assert.Equal(configuration["Excel:SiteHost"] ?? "", options.SiteHost);
        Assert.Equal(configuration["Excel:SitePath"] ?? "", options.SitePath);
        Assert.Equal(configuration["Excel:FolderPath"] ?? "", options.FolderPath);
        Assert.Contains("\"SiteHost\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SitePath\"", json, StringComparison.Ordinal);
        Assert.Contains("\"FolderPath\"", json, StringComparison.Ordinal);
        Assert.Equal(3, options.RetryCount);
        Assert.Equal(5, options.RetryDelaySeconds);
        Assert.Equal("", options.TenantId);
        Assert.Equal("", options.ClientId);
        Assert.Equal("", options.ClientSecret);
        Assert.Equal(3, int.Parse(configuration["Nav:RetryCount"]!));
        Assert.Contains("\"ExcelTenantId\": \"\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ExcelClientId\": \"\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ExcelClientSecret\": \"\"", json, StringComparison.Ordinal);
        Assert.Contains("https://api.onlineszamla.nav.gov.hu", json, StringComparison.Ordinal);
        Assert.Contains("info@nexande.com", json, StringComparison.Ordinal);
        Assert.Equal(WorkerComposition.UserSecretsId, "8f4e2c1a-6b7d-4e9f-a1c2-3d4e5f607182");
        Assert.Contains("OsaPro.Excel", File.ReadAllText(FindRepoFile(Path.Combine("src", "OsaPro.Worker", "OsaPro.Worker.csproj"))), StringComparison.Ordinal);
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

        throw new InvalidOperationException($"A fájl nem található: {relative}");
    }
}
