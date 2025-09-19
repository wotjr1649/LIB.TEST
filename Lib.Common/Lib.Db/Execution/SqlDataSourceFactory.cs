using System;
using System.Collections.Concurrent;
using Lib.Db.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lib.Db.Execution;

/// <summary>
/// 연결 문자열 이름별 <see cref="SqlDataSource"/> 를 캐싱하고 관리합니다.
/// </summary>
internal sealed class SqlDataSourceFactory : IDisposable
{
    private readonly IOptionsMonitor<DbOptions> _options;
    private readonly ILogger<SqlDataSourceFactory> _logger;
    private readonly ConcurrentDictionary<string, SqlDataSource> _sources = new(StringComparer.OrdinalIgnoreCase);

    public SqlDataSourceFactory(IOptionsMonitor<DbOptions> options, ILogger<SqlDataSourceFactory> logger)
    {
        _options = options;
        _logger = logger;

        _options.OnChange((_, _) =>
        {
            // 옵션이 변경되면 모든 DataSource를 폐기하여 새로운 연결 문자열이 반영되도록 한다.
            foreach (var key in _sources.Keys)
            {
                if (_sources.TryRemove(key, out var dataSource))
                {
                    dataSource.Dispose();
                    _logger.LogInformation("Disposed SqlDataSource for connection '{Connection}'.", key);
                }
            }
        });
    }

    public SqlDataSource GetDataSource(string? connectionName)
    {
        var key = string.IsNullOrWhiteSpace(connectionName)
            ? _options.CurrentValue.DefaultConnectionName
            : connectionName!;

        return _sources.GetOrAdd(key, CreateDataSource);
    }

    private SqlDataSource CreateDataSource(string connectionName)
    {
        var options = _options.CurrentValue;
        if (!options.ConnectionStrings.TryGetValue(connectionName, out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionName}' was not found. Ensure it is defined in appsettings.json or configured via DbOptions.");
        }

        _logger.LogInformation("Creating SqlDataSource for connection '{Connection}'.", connectionName);
        return SqlDataSource.Create(connectionString);
    }

    public void Dispose()
    {
        foreach (var pair in _sources)
        {
            pair.Value.Dispose();
        }
        _sources.Clear();
    }
}

