# 数据库设计

## 数据库职责

### Operations（SQL Server，主库）

保存运营后台自身数据：账户与权限、菜单、文件元数据、后台任务、操作日志、邮件模板，以及后续批发运营流程数据。

### Eggrack（MySQL，业务数据源）

读取既有 Eggrack 客户、商品、申请等数据。默认不跨库写入、不建立分布式事务；同步或引用时在 SQL Server 保存来源主键和来源版本。

## 通用约定

- 主键使用 `bigint identity`，关联键同为 `bigint`。
- 时间统一保存 UTC，列名以 `Utc` 结尾。
- 业务表使用软删除 `IsDeleted`；关系表和大体量日志按需要物理清理。
- 权限码采用 `模块.资源.动作`，例如 `system.user.view`。
- 所有唯一索引应排除软删除数据，避免历史记录阻塞重新创建。
- 日志不得保存密码、Token、完整连接串或其他敏感信息。

## 初始表

| 范围 | 表 |
|---|---|
| 权限 | SysUsers, SysRoles, SysPermissions, SysUserRoles, SysRolePermissions |
| 菜单 | SysMenus |
| 文件 | FileAssets |
| 任务 | BackgroundTasks |
| 日志 | OperationLogs |
| 邮件 | EmailTemplates |

批发业务表将在确认旧库字段和实际流程后单独设计，避免现在猜测字段。
