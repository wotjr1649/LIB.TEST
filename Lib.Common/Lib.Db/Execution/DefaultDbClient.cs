using System;
using System.Data;
using System.Globalization;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Lib.Db.Abstractions;
using Lib.Db.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Lib.Db.Execution;

/// <summary>
/// DbDataSource + Polly 파이프라인을 이용해 실제 데이터베이스 명령을 수행하는 기본 구현입니다.
/// </summary>
internal sealed class DefaultDbClient : IDbClient
{
    private readonly IOptionsMonitor<DbOptions> _dbOptions;
    private readonly SqlDataSourceFactory _dataSourceFactory;
    private readonly IDbResiliencePipelineProvider _pipelineProvider;
    private readonly ILogger<DefaultDbClient> _logger;

    private static readonly ResiliencePropertyKey<string> ConnectionNameKey = new("Lib.Db.ConnectionName");
    private static readonly ResiliencePropertyKey<string> CommandTextKey = new("Lib.Db.CommandText");
    private static readonly ResiliencePropertyKey<CommandType> CommandTypeKey = new("Lib.Db.CommandType");
    private static readonly ResiliencePropertyKey<object?> QueryTagKey = new("Lib.Db.Tag");

    public DefaultDbClient(
        IOptionsMonitor<DbOptions> dbOptions,
        SqlDataSourceFactory dataSourceFactory,
        IDbResiliencePipelineProvider pipelineProvider,
        ILogger<DefaultDbClient> logger)
    {
        _dbOptions = dbOptions;
        _dataSourceFactory = dataSourceFactory;
        _pipelineProvider = pipelineProvider;
        _logger = logger;
    }

    public Task<int> ExecuteNonQueryAsync(QueryDefinition query, CancellationToken cancellationToken = default)
    {
        return ExecuteWithPipelineAsync(query, static async (command, token) =>
        {
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<T?> ExecuteScalarAsync<T>(QueryDefinition query, CancellationToken cancellationToken = default)
    {
        return ExecuteWithPipelineAsync(query, static async (command, token) =>
        {
            var result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return ConvertScalar<T>(result);
        }, cancellationToken);
    }

    public IAsyncEnumerable<T> QueryAsync<T>(QueryDefinition query, Func<IDataRecord, T> projector, CancellationToken cancellationToken = default)
    {
        return ExecuteReaderAsync(query, projector, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dataSourceFactory.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<TResult> ExecuteWithPipelineAsync<TResult>(
        QueryDefinition query,
        Func<DbCommand, CancellationToken, Task<TResult>> executor,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var optionsSnapshot = _dbOptions.CurrentValue;
        var connectionName = string.IsNullOrWhiteSpace(query.ConnectionName)
            ? optionsSnapshot.DefaultConnectionName
            : query.ConnectionName!;

        var dataSource = _dataSourceFactory.GetDataSource(connectionName);
        var pipeline = await _pipelineProvider.GetPipelineAsync(connectionName, cancellationToken).ConfigureAwait(false);

        return await pipeline.ExecuteAsync(async ValueTask<TResult> (context) =>
        {
            if (string.IsNullOrWhiteSpace(context.OperationKey))
            {
                context.OperationKey = query.CommandText;
            }

            context.Properties.Set(ConnectionNameKey, connectionName);
            context.Properties.Set(CommandTextKey, query.CommandText);
            context.Properties.Set(CommandTypeKey, query.CommandType);
            if (query.Tag is not null)
            {
                context.Properties.Set(QueryTagKey, query.Tag);
            }

            var token = context.CancellationToken;

            await using var connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);

            DbTransaction? transaction = null;
            try
            {
                var effectiveIsolation = query.IsolationLevel ?? optionsSnapshot.DefaultIsolationLevel;
                if (effectiveIsolation != IsolationLevel.Unspecified)
                {
                    transaction = await connection.BeginTransactionAsync(effectiveIsolation, token).ConfigureAwait(false);
                }

                await using var command = CreateCommand(connection, transaction, optionsSnapshot, query);
                var result = await executor(command, token).ConfigureAwait(false);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                {
                    try
                    {
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogWarning(rollbackEx, "Rollback failed after exception '{Message}'.", ex.Message);
                    }
                }

                throw;
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static T? ConvertScalar<T>(object? value)
    {
        if (value is null || value is DBNull)
        {
            return default;
        }

        var targetType = typeof(T);
        if (targetType == typeof(object) || targetType.IsInstanceOfType(value))
        {
            return (T)value;
        }

        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying is not null)
        {
            var converted = ConvertScalarCore(value, underlying);
            return converted is null ? default : (T)converted;
        }

        return (T)ConvertScalarCore(value, targetType)!;
    }

    private static object? ConvertScalarCore(object value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(Guid))
        {
            return value switch
            {
                Guid guid => guid,
                string text => Guid.Parse(text, CultureInfo.InvariantCulture),
                byte[] bytes => new Guid(bytes),
                ReadOnlyMemory<byte> rom => new Guid(rom.ToArray()),
                Memory<byte> mem => new Guid(mem.Span),
                _ => new Guid(Convert.ToString(value, CultureInfo.InvariantCulture)!)
            };
        }

        if (targetType == typeof(byte[]))
        {
            return value switch
            {
                byte[] bytes => bytes,
                ReadOnlyMemory<byte> rom => rom.ToArray(),
                Memory<byte> mem => mem.ToArray(),
                _ => throw new InvalidCastException($"값 '{value}'(형식 {value.GetType()})을 byte[]로 변환할 수 없습니다.")
            };
        }

        if (targetType.IsEnum)
        {
            if (value is string enumName)
            {
                return Enum.Parse(targetType, enumName, ignoreCase: true);
            }

            var numeric = Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture);
            return Enum.ToObject(targetType, numeric!);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private async IAsyncEnumerable<T> ExecuteReaderAsync<T>(
        QueryDefinition query,
        Func<IDataRecord, T> projector,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = await ExecuteWithPipelineAsync(query, async (command, token) =>
        {
            var buffer = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                buffer.Add(projector(reader));
            }
            return buffer;
        }, cancellationToken).ConfigureAwait(false);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, DbOptions options, QueryDefinition query)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = query.CommandText;
        command.CommandType = query.CommandType;

        var timeout = query.CommandTimeout ?? options.CommandTimeout;
        if (timeout <= TimeSpan.Zero)
        {
            command.CommandTimeout = 0; // 무한대
        }
        else
        {
            command.CommandTimeout = (int)Math.Ceiling(timeout.TotalSeconds);
        }

        foreach (var parameter in query.Parameters)
        {
            var sqlParameter = command.CreateParameter();
            sqlParameter.ParameterName = parameter.Name;
            sqlParameter.Value = parameter.Value ?? DBNull.Value;
            if (parameter.DbType.HasValue)
            {
                sqlParameter.DbType = parameter.DbType.Value;
            }
            if (parameter.Size.HasValue)
            {
                sqlParameter.Size = parameter.Size.Value;
            }
            if (parameter.Precision.HasValue)
            {
                sqlParameter.Precision = parameter.Precision.Value;
            }
            if (parameter.Scale.HasValue)
            {
                sqlParameter.Scale = parameter.Scale.Value;
            }
            sqlParameter.Direction = parameter.Direction;
            command.Parameters.Add(sqlParameter);
        }

        return command;
    }
}


