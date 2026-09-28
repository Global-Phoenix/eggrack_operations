# 数据库访问

## 多数据库配置

`Database:SqlServer:Connections` 和 `Database:MySql:Connections` 都是命名字典，可分别增加任意数量的数据源。业务代码必须显式传入名称，避免误用默认数据库。

连接串放在 User Secrets、环境变量或被忽略的 `appsettings.Local.json` 中。禁止把密码提交到仓库。

## 查询与存储过程

```csharp
public sealed class OrderQuery(DatabaseSessionFactory databases)
{
    public async Task<IReadOnlyList<OrderRow>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var session = await databases.OpenSqlServerAsync("Orders", cancellationToken);
        return await session.QueryStoredProcedureAsync<OrderRow>(
            "dbo.usp_Order_List",
            new { Status = "Pending" },
            cancellationToken);
    }
}
```

MySQL 使用 `OpenMySqlAsync("Eggrack")`。普通 SQL 使用 `QueryAsync<T>` 或 `ExecuteAsync`；存储过程使用 `QueryStoredProcedureAsync<T>` 或 `ExecuteStoredProcedureAsync`。

Dapper 的 `DynamicParameters` 可用于输入、输出和返回值参数。

## 事务

```csharp
await using var session = await databases.OpenSqlServerAsync("Orders", cancellationToken);
await session.BeginTransactionAsync(cancellationToken: cancellationToken);
try
{
    await session.ExecuteAsync(firstSql, firstArgs, cancellationToken: cancellationToken);
    await session.ExecuteStoredProcedureAsync("dbo.usp_UpdateSummary", summaryArgs, cancellationToken);
    await session.CommitAsync(cancellationToken);
}
catch
{
    await session.RollbackAsync(cancellationToken);
    throw;
}
```

事务只覆盖同一个 `DatabaseSession` 的同一物理连接。不同数据库之间不使用分布式事务；需要跨库处理时采用本地主事务、幂等任务和补偿机制。
