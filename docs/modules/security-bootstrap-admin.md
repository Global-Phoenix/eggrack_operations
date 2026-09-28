# 首位超级管理员初始化

此工具仅用于创建首位本地 Identity 用户，并同步创建权限人员、绑定系统角色
`super_admin`。密码通过当前终端隐藏输入，不写入命令历史、配置文件或 Git。

在仓库根目录执行：

```powershell
dotnet run --project tools/Eggrack.Security.Bootstrap -- `
  --config .\eggrack_operations.Web\appsettings.Local.json `
  --username admin `
  --email admin@example.com `
  --display-name "系统管理员"
```

密码至少 12 位，并同时包含大写字母、小写字母、数字和特殊字符。创建完成后，
首次登录会强制绑定 TOTP，并显示一次性恢复代码。

如果 Identity 创建、人员创建或角色绑定任一步骤失败，工具会停止并回滚本次创建。
账号创建成功后，不应继续共享此初始化终端。
