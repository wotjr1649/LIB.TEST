using System.Collections.Concurrent;
using Lib.Db.Abstractions;
using Lib.Db.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Lib.Db.Resilience;

/// <summary>
/// 연결 이름별로 Polly 복원력 파이프라인을 구성하고 캐시합니다.
/// </summary>
internal sealed class DbResiliencePipelineProvider : IDbResiliencePipelineProvider
{
    private readonly IOptionsMonitor<DbResilienceOptions> _options;
    private readonly ILogger<DbResiliencePipelineProvider> _logger;
    private readonly ConcurrentDictionary<string, ResiliencePipeline> _pipelines = new(StringComparer.OrdinalIgnoreCase);

    public DbResiliencePipelineProvider(IOptionsMonitor<DbResilienceOptions> options, ILogger<DbResiliencePipelineProvider> logger)
    {
        _options = options;
        _logger = logger;

        _options.OnChange((snapshot, name) =>
        {
            var key = Normalize(name);
            _pipelines.TryRemove(key, out _);
            _logger.LogInformation("Resilience pipeline cache invalidated for connection '{Connection}'.", key);
        });
    }

    public ValueTask<ResiliencePipeline> GetPipelineAsync(string? connectionName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Normalize(connectionName);
        var pipeline = _pipelines.GetOrAdd(key, BuildPipeline);
        return ValueTask.FromResult(pipeline);
    }

    private ResiliencePipeline BuildPipeline(string connectionName)
    {
        var options = _options.Get(connectionName);
        if (!options.Enabled)
        {
            _logger.LogDebug("Resilience disabled for connection '{Connection}'. Returning empty pipeline.", connectionName);
            return ResiliencePipeline.Empty;
        }

        var builder = new ResiliencePipelineBuilder
        {
            Name = $"Lib.Db[{connectionName}]"
        };

        if (options.Retry.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                Delay = options.Retry.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.Retry.UseJitter,
                OnRetry = args =>
                {
                    if (args.Outcome.Exception is { } ex)
                    {
                        _logger.LogWarning(ex, "Retry attempt {Attempt} for connection '{Connection}'.", args.AttemptNumber, connectionName);
                    }
                    return ValueTask.CompletedTask;
                }
            });
        }

        if (options.Timeout.Enabled && options.Timeout.PerAttempt > TimeSpan.Zero)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.Timeout.PerAttempt,
                OnTimeout = _ =>
                {
                    _logger.LogWarning("Timeout reached for connection '{Connection}' after {Timeout}.", connectionName, options.Timeout.PerAttempt);
                    return ValueTask.CompletedTask;
                }
            });
        }

        if (options.CircuitBreaker.Enabled)
        {
            _logger.LogDebug("Circuit breaker configuration detected for connection '{Connection}', pending implementation.", connectionName);
        }

        if (options.Bulkhead.Enabled || options.RateLimiter.Enabled)
        {
            _logger.LogDebug("Bulkhead or rate limiter configuration detected for connection '{Connection}', pending implementation.", connectionName);
        }

        var pipeline = builder.Build();
        _logger.LogInformation("Constructed resilience pipeline '{Name}'.", builder.Name);
        return pipeline;
    }

    private static string Normalize(string? connectionName)
        => string.IsNullOrWhiteSpace(connectionName) ? Microsoft.Extensions.Options.Options.DefaultName : connectionName;
}

