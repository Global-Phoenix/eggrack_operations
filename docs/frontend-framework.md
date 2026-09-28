# MVC 前端框架约定

项目使用 .NET 8 MVC、Razor、Bootstrap 5 和 Bootstrap Icons，不采用前后端分离。

## 公共组件

- PageHeader：统一页面标题、说明、面包屑、状态和主次操作按钮。
- EmptyState：统一空数据、说明和引导操作。
- Navigation：统一侧栏导航及子菜单。
- Topbar：统一页签、页签批量操作和用户入口。
- ModuleGrid：工作台模块入口。

## 页面结构类

- ui-panel、ui-panel-header、ui-panel-body：标准内容面板。
- ui-toolbar：列表筛选及批量操作区。
- ui-data-table：统一可横向滚动的数据表格。
- ui-pagination：统一分页区域。
- ui-skeleton：统一加载占位。
- ui-status-success、ui-status-warning、ui-status-danger：状态提示。

业务页面只保留模块专属结构和样式，通用视觉不应在页面内重复定义。页签状态保存在浏览器 sessionStorage，首页固定，并支持刷新当前页、关闭右侧、关闭其他和关闭全部。
