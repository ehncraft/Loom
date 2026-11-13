using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Loom.AuthZen;
using Loom.Caveats;
using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loom.Tests;

public sealed class LoomAuthZenEndpointsTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private IHost? _host;
    private HttpClient? _client;

    private static LoomEvaluator BuildEvaluator()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation viewer: user
                relation restricted_viewer: user
                permission view = viewer
                permission restricted_view = restricted_viewer
            }
            """);

        var caveats = CaveatParser.Parse(
            """
            caveat match_region(expected_regions list<string>, observed_region string) {
              observed_region in expected_regions
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "INV-1"), "viewer", new SubjectRef("user", "alice")),
            new RelationTuple(
                new ObjectRef("document", "INV-1"),
                "restricted_viewer",
                new SubjectRef("user", "alice"),
                CaveatName: "match_region",
                CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expected_regions"] = new List<string> { "eu-west-1" },
                }),
        ]);

        return new LoomEvaluator(schema, tuples, caveats);
    }

    public async Task InitializeAsync()
    {
        var builder = new HostBuilder().ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(BuildEvaluator());
            });
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapAuthZenEndpoints());
            });
        });

        _host = await builder.StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public async Task Evaluation_PermittedSubject_ReturnsDecisionTrue()
    {
        var request = new
        {
            subject = new { type = "user", id = "alice" },
            resource = new { type = "document", id = "INV-1" },
            action = new { name = "view" },
        };

        var response = await _client!.PostAsJsonAsync("/access/v1/evaluation", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("decision").GetBoolean());
    }

    [Fact]
    public async Task Evaluation_DeniedSubject_ReturnsDecisionFalse()
    {
        var request = new
        {
            subject = new { type = "user", id = "bob" },
            resource = new { type = "document", id = "INV-1" },
            action = new { name = "view" },
        };

        var response = await _client!.PostAsJsonAsync("/access/v1/evaluation", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("decision").GetBoolean());
    }

    [Fact]
    public async Task Evaluation_CaveatedWithoutContext_ReturnsDecisionFalseWithMissingContext()
    {
        var request = new
        {
            subject = new { type = "user", id = "alice" },
            resource = new { type = "document", id = "INV-1" },
            action = new { name = "restricted_view" },
        };

        var response = await _client!.PostAsJsonAsync("/access/v1/evaluation", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("decision").GetBoolean());
        var missing = body.GetProperty("context").GetProperty("missing_context");
        Assert.Equal("observed_region", missing[0].GetString());
    }

    [Fact]
    public async Task Evaluation_CaveatedWithSatisfyingContext_ReturnsDecisionTrue()
    {
        var request = new
        {
            subject = new { type = "user", id = "alice" },
            resource = new { type = "document", id = "INV-1" },
            action = new { name = "restricted_view" },
            context = new { observed_region = "eu-west-1" },
        };

        var response = await _client!.PostAsJsonAsync("/access/v1/evaluation", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("decision").GetBoolean());
    }

    [Fact]
    public async Task Evaluation_MissingRequiredField_ReturnsBadRequest()
    {
        using var content = JsonContent.Create(
            new { resource = new { type = "document", id = "INV-1" } }, options: JsonOptions);

        var response = await _client!.PostAsync("/access/v1/evaluation", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Evaluations_ExecuteAll_RunsEveryEntry()
    {
        var request = new
        {
            subject = new { type = "user", id = "alice" },
            resource = new { type = "document", id = "INV-1" },
            evaluations = new object[]
            {
                new { action = new { name = "view" } },
                new { action = new { name = "restricted_view" } },
            },
        };

        var response = await _client!.PostAsJsonAsync("/access/v1/evaluations", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var evaluations = body.GetProperty("evaluations");
        Assert.Equal(2, evaluations.GetArrayLength());
        Assert.True(evaluations[0].GetProperty("decision").GetBoolean());
        Assert.False(evaluations[1].GetProperty("decision").GetBoolean());
    }

    [Fact]
    public async Task Evaluations_DenyOnFirstDeny_StopsAfterFirstDeny()
    {
        var request = new
        {
            subject = new { type = "user", id = "bob" },
            resource = new { type = "document", id = "INV-1" },
            evaluations = new object[]
            {
                new { action = new { name = "view" } },
                new { action = new { name = "restricted_view" } },
            },
            options = new { evaluations_semantic = "deny_on_first_deny" },
        };

        var response = await _client!.PostAsJsonAsync("/access/v1/evaluations", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var evaluations = body.GetProperty("evaluations");
        Assert.Equal(1, evaluations.GetArrayLength());
        Assert.False(evaluations[0].GetProperty("decision").GetBoolean());
    }
}
