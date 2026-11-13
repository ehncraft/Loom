using System.Text.Json;

using Loom.Evaluator;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Loom.AuthZen;

public static class EndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IEndpointRouteBuilder MapLoomHealthChecks(this IEndpointRouteBuilder endpoints, string path = "/health")
    {
        endpoints.MapHealthChecks(path);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapAuthZenEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/access/v1")
    {
        endpoints.MapPost($"{prefix}/evaluation", EvaluateAsync);
        endpoints.MapPost($"{prefix}/evaluations", EvaluateBatchAsync);
        return endpoints;
    }

    private static async Task<IResult> EvaluateAsync(HttpContext http, LoomEvaluator evaluator, CancellationToken cancellationToken)
    {
        AuthZenEvaluationRequest request;
        try
        {
            request = await http.Request.ReadFromJsonAsync<AuthZenEvaluationRequest>(JsonOptions, cancellationToken)
                ?? throw new JsonException("Request body is required.");
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var response = await EvaluateOneAsync(
            evaluator, request.Subject, request.Resource, request.Action, request.Context, cancellationToken);
        return Results.Json(response, JsonOptions);
    }

    private static async Task<IResult> EvaluateBatchAsync(
        HttpContext http, LoomEvaluator evaluator, CancellationToken cancellationToken)
    {
        AuthZenEvaluationsRequest request;
        try
        {
            request = await http.Request.ReadFromJsonAsync<AuthZenEvaluationsRequest>(JsonOptions, cancellationToken)
                ?? throw new JsonException("Request body is required.");
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var semantic = request.Options?.EvaluationsSemantic ?? "execute_all";
        var results = new List<AuthZenEvaluationResponse>();

        foreach (var evaluation in request.Evaluations)
        {
            var subject = evaluation.Subject ?? request.Subject;
            var resource = evaluation.Resource ?? request.Resource;
            var action = evaluation.Action ?? request.Action;
            var context = evaluation.Context ?? request.Context;

            if (subject is null || resource is null || action is null)
            {
                return Results.BadRequest(
                    "Each evaluation must resolve a subject, resource, and action, either per-entry or as a request-level default.");
            }

            var response = await EvaluateOneAsync(evaluator, subject, resource, action, context, cancellationToken);
            results.Add(response);

            if (semantic == "deny_on_first_deny" && !response.Decision)
            {
                break;
            }

            if (semantic == "permit_on_first_permit" && response.Decision)
            {
                break;
            }
        }

        return Results.Json(new AuthZenEvaluationsResponse { Evaluations = results }, JsonOptions);
    }

    private static async Task<AuthZenEvaluationResponse> EvaluateOneAsync(
        LoomEvaluator evaluator,
        AuthZenEntity subject,
        AuthZenEntity resource,
        AuthZenAction action,
        Dictionary<string, JsonElement>? context,
        CancellationToken cancellationToken)
    {
        var result = await evaluator.CheckAsync(
            AuthZenMapping.ToObjectRef(resource),
            action.Name,
            AuthZenMapping.ToSubjectRef(subject),
            AuthZenMapping.ToContext(context),
            cancellationToken);
        return AuthZenMapping.ToResponse(result);
    }
}
