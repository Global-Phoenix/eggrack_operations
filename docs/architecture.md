# Eggrack Operations 架构

## 技术路线

- .NET 8 / ASP.NET Core MVC / Razor Views
- Bootstrap 5（静态资源随项目发布，不依赖前端构建）
- Dapper 作为数据访问基础组件
- SQL Server 作为运营后台主库
- MySQL 作为现有 Eggrack 业务数据源
- xUnit 用于应用服务和数据访问测试

## 依赖方向

```text
eggrack_operations.Web
  ├─ eggrack_operations.Application
  └─ eggrack_operations.Infrastructure
       ├─ eggrack_operations.Application
       ├─ eggrack_operations.Domain
       └─ eggrack_operations.Common

eggrack_operations.Application
  ├─ eggrack_operations.Domain
  └─ eggrack_operations.Common
```

- Web：Controller、Razor View、认证授权、MVC 过滤器、启动配置。
- Application：用例服务、DTO、权限判断、菜单编排和事务边界接口。
- Domain：实体、值对象、枚举与领域规则，不引用数据库或 Web。
- Infrastructure：Dapper、SQL Server/MySQL、文件存储、邮件和后台任务实现。
- Common：分页、统一结果、公共异常和跨层基础类型。
- Tests：单元测试和后续集成测试。

## 模块边界

1. 批发模块：读取 MySQL/既有业务库，运营流程数据写入 SQL Server。
2. 权限模块：用户、角色、权限、用户角色、角色权限。
3. 文件中心：文件元数据写 SQL Server，文件内容通过存储提供程序保存。
4. 任务中心：导入、导出、同步等长任务及执行状态。
5. 日志中心：操作审计、登录审计、任务和异常关联追踪。
6. 菜单管理：菜单树、路由信息、显示顺序与权限码。
7. 邮件模板：主题、HTML/文本正文、变量和版本信息。

## 配置与安全

仓库中的数据库连接默认关闭，且不保存连接字符串。开发机使用 User Secrets：

```powershell
dotnet user-secrets init --project .\eggrack_operations.Web
dotnet user-secrets set "ConnectionStrings:OperationsConnection" "<SQL Server>" --project .\eggrack_operations.Web
dotnet user-secrets set "ConnectionStrings:EggrackConnection" "<MySQL>" --project .\eggrack_operations.Web
```

启用连接时，在未提交的 `eggrack_operations.Web/appsettings.Local.json` 中把相应 `Enabled` 改为 `true`。
