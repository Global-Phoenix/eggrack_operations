using System.Data;
using System.Data.Common;
using Dapper;

namespace Eggrack.Operations.Infrastructure.Database;

public sealed class DatabaseSession : IAsyncDisposable
{
    private readonly int _commandTimeoutSeconds;
    private DbTransaction? _transaction;

    internal DatabaseSession(DbConnection connection, int commandTimeoutSeconds)
    {
        Connection = connection;
        _commandTimeoutSeconds = commandTimeoutSeconds;
    }

    public DbConnection Connection { get; }
    public DbTransaction? Transaction => _transaction;

    public async Task BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        if (_transaction is not null) throw new InvalidOperationException("当前会话已开启事务");
        _transaction = await Connection.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public Task<int> ExecuteAsync(
        string commandText,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default) =>
        Connection.ExecuteAsync(CreateCommand(commandText, parameters, commandType, cancellationToken));

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string commandText,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        var rows = await Connection.QueryAsync<T>(CreateCommand(commandText, parameters, commandType, cancellationToken));
        return rows.AsList();
    }

    public Task<int> ExecuteStoredProcedureAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(procedureName, parameters, CommandType.StoredProcedure, cancellationToken);

    public Task<IReadOnlyList<T>> QueryStoredProcedureAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default) =>
        QueryAsync<T>(procedureName, parameters, CommandType.StoredProcedure, cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) throw new InvalidOperationException("当前会话没有活动事务");
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        await Connection.DisposeAsync();
    }

    private CommandDefinition CreateCommand(
        string commandText,
        object? parameters,
        CommandType commandType,
        CancellationToken cancellationToken) =>
        new(commandText, parameters, _transaction, _commandTimeoutSeconds, commandType, cancellationToken: cancellationToken);
}
