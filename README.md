# eggrack_operations

.NET 8 MVC + Bootstrap 5 运营管理系统。

## 使用 Visual Studio 2022

1. 安装 VS2022 17.8 或更高版本及“ASP.NET 和 Web 开发”工作负载。
2. 打开 `eggrack_operations.sln`。
3. 将 `eggrack_operations.Web` 设为启动项目。
4. 选择 `https` 或 `IIS Express` 配置并按 F5。

项目默认不连接数据库，可直接启动界面。数据库连接串使用 User Secrets；配置说明见 `docs/architecture.md`。

## 命令行验证

```powershell
dotnet restore
dotnet build -c Debug
dotnet test -c Debug
dotnet run --project .\eggrack_operations.Web --launch-profile https
```

数据库初始化脚本：`database/sqlserver/001_initial_platform.sql`。
