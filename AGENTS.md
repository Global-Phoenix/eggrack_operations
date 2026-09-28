# AGENTS.md

## 并行开发

- 每个开发窗口必须使用独立 Git worktree 和 `feature/<module>-<task>` 分支。
- 一个窗口只负责一个模块；模块代码放在各层的 `Modules/<Module>/` 或 Web 的 `Areas/<Module>/`。
- 模块窗口不要修改 Program.cs、解决方案、共享布局、全局 CSS、公共配置和公共数据库脚本；这些由集成窗口处理。
- UI 优先使用 ViewComponent/Partial 封装；后端默认使用具体类，不机械创建接口。
- 数据访问统一使用 DatabaseSessionFactory/DatabaseSession，并显式指定命名数据库。
- 提交前运行 Debug 构建和相关测试，禁止提交 appsettings.Local.json、密钥或连接串。
- 详细规范见 `.cursor/rules/` 和 `docs/parallel-development.md`。
