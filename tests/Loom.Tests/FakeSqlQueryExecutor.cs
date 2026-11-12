using Loom.Spool;

namespace Loom.Tests;

internal sealed class FakeSqlQueryExecutor(
    params IReadOnlyList<IReadOnlyDictionary<string, object?>>[] queryResponses) : ISqlQueryExecutor
{
    private readonly Queue<IReadOnlyList<IReadOnlyDictionary<string, object?>>> _queryResponses = new(queryResponses);

    public List<(string CommandText, IReadOnlyDictionary<string, object?> Parameters)> ExecuteCalls { get; } = [];

    public List<(string CommandText, IReadOnlyDictionary<string, object?> Parameters)> QueryCalls { get; } = [];

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string commandText, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        QueryCalls.Add((commandText, parameters));
        var response = _queryResponses.Count > 0
            ? _queryResponses.Dequeue()
            : Array.Empty<IReadOnlyDictionary<string, object?>>();
        return Task.FromResult(response);
    }

    public Task ExecuteAsync(
        string commandText, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        ExecuteCalls.Add((commandText, parameters));
        return Task.CompletedTask;
    }
}
