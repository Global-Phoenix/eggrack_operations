# 开发约定

## Razor 与前端结构

- 页面布局区域和具有独立数据来源的可复用区块优先使用 ViewComponent。
- 纯展示、无独立逻辑的短片段使用 Partial View。
- 列表筛选、分页、状态标签、确认弹窗等重复结构封装后复用，不复制大段 HTML。
- View 中只做展示和轻量格式化；查询、权限判断和业务规则放在应用服务。
- Bootstrap 5 为默认 UI 基础，不引入前后端分离框架。

## 后端抽象

- 默认使用具体类，不为每个 Service、Repository 机械创建接口。
- 只有存在多个实现、需要隔离外部系统、需要运行时替换，或测试确实需要替身时才增加接口。
- 优先按业务能力封装完整方法，避免 Controller 组合多段重复的数据访问流程。
- 公共逻辑集中到 Application/Infrastructure；不要建立只有转发作用的空层。
- Controller 保持薄：绑定输入、调用应用服务、返回 View/Redirect。

## 命名与模块

- ViewComponent 使用 `{Name}ViewComponent`，视图位于 `Views/Shared/Components/{Name}/Default.cshtml`。
- 权限码统一为 `模块.资源.动作`。
- 数据库连接、文件存储和邮件发送属于合理的基础设施边界，可在确有替换需要时抽象。
