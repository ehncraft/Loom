using Loom.Schema;

namespace Loom.Spool;

public interface ISqlSchemaStore
{
    Task<LoomSchema?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(string schemaText, CancellationToken cancellationToken);
}
