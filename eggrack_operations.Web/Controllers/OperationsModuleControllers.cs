using eggrack_operations.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Controllers;

[Authorize]
public sealed class WholesaleController : Controller
{
    public IActionResult Orders() => View("~/Views/Shared/ModuleListPage.cshtml", new ModuleListPageViewModel(
        "批发计划", "统一查看客户订单、金额、交付节点与订单状态", "批发管理", "bi-receipt",
        "搜索订单号、客户或产品", "新增订单", ["待确认", "处理中", "已完成", "已取消"],
        [new("number", "订单号", "150px"), new("customer", "客户"), new("product", "产品"), new("amount", "订单金额"), new("delivery", "预计交付"), new("owner", "负责人")],
        [
            Row("WO-20260928-018", "待确认", "warning", ("number","WO-20260928-018"), ("customer","North Star Market"), ("product","折叠收纳箱 × 1,200"), ("amount","$8,640.00"), ("delivery","2026-10-18"), ("owner","王芳")),
            Row("WO-20260927-011", "处理中", "success", ("number","WO-20260927-011"), ("customer","Urban Living"), ("product","竹制厨房套装 × 800"), ("amount","$12,480.00"), ("delivery","2026-10-25"), ("owner","刘洋")),
            Row("WO-20260922-006", "已完成", "neutral", ("number","WO-20260922-006"), ("customer","Home Select"), ("product","浴室置物架 × 600"), ("amount","$5,220.00"), ("delivery","2026-09-26"), ("owner","陈晨"))
        ]));

    private static ModuleListRow Row(string id, string status, string tone, params (string Key, string Value)[] values) =>
        new(id, values.ToDictionary(x => x.Key, x => x.Value), status, tone);
}

[Authorize]
public sealed class TasksController : Controller
{
    public IActionResult Index() => View("~/Views/Shared/ModuleListPage.cshtml", new ModuleListPageViewModel(
        "任务中心", "集中跟踪导入、导出、邮件和后台处理任务", "运营工具", "bi-list-task",
        "搜索任务编号或任务名称", "创建任务", ["等待中", "执行中", "已完成", "失败"],
        [new("number","任务编号","150px"),new("name","任务名称"),new("type","类型"),new("progress","进度"),new("creator","创建人"),new("created","创建时间")],
        [
            Row("TASK-260928-009","执行中","warning",("number","TASK-260928-009"),("name","采购报价批量导出"),("type","导出"),("progress","68%"),("creator","王芳"),("created","今天 16:42")),
            Row("TASK-260928-006","已完成","success",("number","TASK-260928-006"),("name","供应商报价提醒"),("type","邮件"),("progress","100%"),("creator","系统"),("created","今天 14:18")),
            Row("TASK-260927-031","失败","danger",("number","TASK-260927-031"),("name","历史订单导入"),("type","导入"),("progress","42%"),("creator","刘洋"),("created","昨天 20:05"))
        ]));
    private static ModuleListRow Row(string id,string status,string tone,params (string Key,string Value)[] values)=>new(id,values.ToDictionary(x=>x.Key,x=>x.Value),status,tone);
}

[Authorize]
public sealed class LogsController : Controller
{
    public IActionResult Index() => View("~/Views/Shared/ModuleListPage.cshtml", new ModuleListPageViewModel(
        "日志中心", "查询用户操作、系统事件和异常记录", "运营工具", "bi-journal-text",
        "搜索模块、操作人或内容", "导出日志", ["信息", "警告", "异常"],
        [new("time","发生时间","170px"),new("module","模块"),new("action","操作"),new("operator","操作人"),new("ip","来源 IP"),new("summary","内容")],
        [
            Row("LOG-1","信息","success",("time","2026-09-28 17:56:20"),("module","文件中心"),("action","查看文件"),("operator","王芳"),("ip","10.0.12.31"),("summary","查看采购申请 PR-260928-018 附件")),
            Row("LOG-2","警告","warning",("time","2026-09-28 17:42:08"),("module","权限管理"),("action","拒绝访问"),("operator","陈晨"),("ip","10.0.12.46"),("summary","缺少 auth.role.write 权限")),
            Row("LOG-3","异常","danger",("time","2026-09-28 16:31:44"),("module","任务中心"),("action","导入失败"),("operator","刘洋"),("ip","10.0.12.52"),("summary","订单文件第 38 行格式错误"))
        ]));
    private static ModuleListRow Row(string id,string status,string tone,params (string Key,string Value)[] values)=>new(id,values.ToDictionary(x=>x.Key,x=>x.Value),status,tone);
}

[Authorize]
public sealed class MenusController : Controller
{
    public IActionResult Index() => View("~/Views/Shared/ModuleListPage.cshtml", new ModuleListPageViewModel(
        "菜单管理", "维护后台导航层级、路由、图标、排序和权限标识", "系统管理", "bi-menu-button-wide",
        "搜索菜单名称、路由或权限", "新增菜单", ["启用", "停用"],
        [new("name","菜单名称"),new("parent","上级菜单"),new("route","路由"),new("permission","权限标识"),new("sort","排序"),new("icon","图标")],
        [
            Row("menu-wholesale","启用","success",("name","批发管理"),("parent","主导航"),("route","—"),("permission","—"),("sort","20"),("icon","bi-cart3")),
            Row("menu-procurement","启用","success",("name","采购开发"),("parent","批发管理"),("route","/wholesale/procurement"),("permission","wholesale.purchase-plan.view"),("sort","10"),("icon","bi-clipboard-check")),
            Row("menu-email","启用","success",("name","邮件模板"),("parent","系统管理"),("route","/EmailTemplates"),("permission","system.email-template.view"),("sort","40"),("icon","bi-envelope-paper"))
        ]));
    private static ModuleListRow Row(string id,string status,string tone,params (string Key,string Value)[] values)=>new(id,values.ToDictionary(x=>x.Key,x=>x.Value),status,tone);
}

[Authorize]
public sealed class EmailTemplatesController : Controller
{
    public IActionResult Index() => View("~/Views/Shared/ModuleListPage.cshtml", new ModuleListPageViewModel(
        "邮件模板", "管理业务邮件主题、正文、变量和启停状态", "系统管理", "bi-envelope-paper",
        "搜索模板编码或名称", "新增模板", ["启用", "草稿", "停用"],
        [new("code","模板编码","180px"),new("name","模板名称"),new("scene","使用场景"),new("subject","邮件主题"),new("updated","更新时间"),new("editor","修改人")],
        [
            Row("purchase-quote","启用","success",("code","PURCHASE_QUOTE"),("name","采购最终报价"),("scene","报价审核通过"),("subject","Quotation #{PlanNumber}"),("updated","2026-09-28 16:20"),("editor","王芳")),
            Row("task-failed","启用","success",("code","TASK_FAILED"),("name","后台任务失败提醒"),("scene","任务执行失败"),("subject","任务 {TaskName} 执行失败"),("updated","2026-09-27 10:05"),("editor","系统管理员")),
            Row("file-expiring","草稿","warning",("code","FILE_EXPIRING"),("name","会员文件到期提醒"),("scene","清理前 7 天"),("subject","文件即将到期"),("updated","2026-09-25 09:18"),("editor","刘静"))
        ]));
    private static ModuleListRow Row(string id,string status,string tone,params (string Key,string Value)[] values)=>new(id,values.ToDictionary(x=>x.Key,x=>x.Value),status,tone);
}