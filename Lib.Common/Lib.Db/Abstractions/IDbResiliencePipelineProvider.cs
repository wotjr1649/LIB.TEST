using Polly;

namespace Lib.Db.Abstractions;

/// <summary>
/// Resolves resilience pipelines that wrap the execution of database commands.
/// </summary>
public interface IDbResiliencePipelineProvider
{
    ValueTask<ResiliencePipeline> GetPipelineAsync(string? connectionName, CancellationToken cancellationToken = default);
}

