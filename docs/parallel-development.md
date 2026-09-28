# 多窗口模块开发

## 窗口与分支

每个 VS2022 窗口打开一个独立 worktree 中的 `eggrack_operations.sln`。不要在同一个目录中同时开发多个模块。

建议模块：`wholesale`、`security`、`files`、`tasks`、`logs`、`menus`、`email-templates`。

创建工作树：

```powershell
.\scripts\New-ModuleWorktree.ps1 -Module security -Task login
```

脚本将在解决方案同级创建 `eggrack_operations-wt-security-login`，并创建分支 `feature/security-login`。在新目录中打开解决方案即可开始另一个窗口。

## 文件归属

模块窗口可修改：

- `eggrack_operations.Domain/Modules/<Module>/`
- `eggrack_operations.Application/Modules/<Module>/`
- `eggrack_operations.Infrastructure/Modules/<Module>/`
- `eggrack_operations.Web/Areas/<Module>/`
- `database/sqlserver/modules/<module>/`
- 对应测试和模块文档

集成窗口专属：解决方案文件、Program.cs、DependencyInjection 根注册、共享布局、Shared 组件、全局 CSS、appsettings.json 和公共数据库脚本。

## 交接格式

模块完成时提供：分支名、最后 commit、功能摘要、数据库脚本、配置项、构建/测试结果、待集成的共享文件改动建议。
