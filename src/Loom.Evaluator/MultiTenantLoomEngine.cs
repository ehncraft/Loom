using System.Collections.Concurrent;

using Loom.Core;

namespace Loom.Evaluator;

public sealed class MultiTenantLoomEngine(Func<string, CancellationToken, Task<LoomEvaluator>> engineFactory)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<LoomEvaluator>>> _engines = new(StringComparer.Ordinal);

    public Task<LoomEvaluator> GetEngineAsync(string tenantId, CancellationToken cancellationToken) =>
        _engines
            .GetOrAdd(tenantId, id => new Lazy<Task<LoomEvaluator>>(() => engineFactory(id, cancellationToken)))
            .Value;

    public async Task<CheckResult> CheckAsync(
        string tenantId,
        ObjectRef resource,
        string permissionOrRelation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var engine = await GetEngineAsync(tenantId, cancellationToken);
        return await engine.CheckAsync(resource, permissionOrRelation, subject, context, cancellationToken);
    }

    public async Task<ExpandNode> ExpandAsync(
        string tenantId, ObjectRef resource, string permissionOrRelation, CancellationToken cancellationToken = default)
    {
        var engine = await GetEngineAsync(tenantId, cancellationToken);
        return await engine.ExpandAsync(resource, permissionOrRelation, cancellationToken);
    }

    public async Task<IReadOnlyList<ObjectRef>> LookupResourcesAsync(
        string tenantId,
        SubjectRef subject,
        string permission,
        string resourceNamespace,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var engine = await GetEngineAsync(tenantId, cancellationToken);
        return await engine.LookupResourcesAsync(subject, permission, resourceNamespace, context, cancellationToken);
    }

    public async Task<IReadOnlyList<SubjectRef>> LookupSubjectsAsync(
        string tenantId,
        ObjectRef resource,
        string permission,
        string subjectNamespace,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var engine = await GetEngineAsync(tenantId, cancellationToken);
        return await engine.LookupSubjectsAsync(resource, permission, subjectNamespace, context, cancellationToken);
    }

    public Task InvalidateAsync(string tenantId)
    {
        _engines.TryRemove(tenantId, out _);
        return Task.CompletedTask;
    }
}
