# 内部人员权限模块

## 已实现

- Security Area：权限概览、人员权限、角色权限页面。
- 当前登录账号映射：优先读取 ClaimTypes.NameIdentifier，其次读取 Identity.Name，对应 eggrack_auth_staff.staff_ref。
- InternalPermissionAttribute：未登录返回 Challenge；没有人员映射或权限返回 Forbid。
- 有效角色、有效期、显式 DENY、数据范围及多部门合并。
- 部门角色授权事务、auth_version 递增和审计日志。

## 集成窗口修改

在 Web 项目 Program.cs 引入 eggrack_operations.Areas.Security，并在 AddInfrastructure 之后调用：

    builder.Services.AddSecurityArea();

项目配置实际登录认证后，在 UseAuthorization 前调用 UseAuthentication。

导航入口：Area=Security、Controller=Security、Action=Index、Permission=auth.staff.read。

## 首个管理员

上线前将已登录账号的稳定唯一标识写入 eggrack_auth_staff.staff_ref，并授予全局 super_admin。不要使用显示名称作为长期账号标识。

