using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Orders.IntegrationTests.Infrastructure;

namespace Orders.IntegrationTests;

[Collection(IntegrationTestEnvironment.Collection)]
public sealed class HealthCheckTests(IntegrationTestEnvironment environment)
{
    public static TheoryData<string> Hosts => ["api", "worker"];

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task Ready_DeveVerificarBancoEMensageria(string host)
    {
        var report = await GetAsync(host, "/health/ready");

        report.Status.Should().Be(HttpStatusCode.OK);
        report.Checks.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["postgres"] = "Healthy",
            ["masstransit-bus"] = "Healthy",
        });
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task Live_DeveVerificarApenasOProcesso(string host)
    {
        var report = await GetAsync(host, "/health/live");

        report.Status.Should().Be(HttpStatusCode.OK);
        report.Checks.Should().BeEquivalentTo(new Dictionary<string, string> { ["self"] = "Healthy" });
    }

    private async Task<(HttpStatusCode Status, Dictionary<string, string> Checks)> GetAsync(string host, string path)
    {
        var client = host == "api" ? environment.Api.CreateClient() : environment.Worker.CreateClient();
        var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = json.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("status").GetString()!);

        return (response.StatusCode, checks);
    }
}
