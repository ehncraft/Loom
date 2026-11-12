namespace Loom.Spool;

public interface ISqlQueryExecutor
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string commandText,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken);

    Task ExecuteAsync(
        string commandText,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken);
}
