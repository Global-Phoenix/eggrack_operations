# Entra ID OIDC 登录部署

## Entra 应用注册

1. 创建单租户 Web 应用注册。
2. 开发回调地址设置为 `https://localhost:<port>/signin-oidc`。
3. 生产回调地址设置为 `https://<正式域名>/signin-oidc`。
4. 登出回调设置为 `https://<域名>/signout-callback-oidc`。
5. 生产环境优先使用证书凭据；如使用 Client Secret，只存入密钥库或环境变量。

配置键为 `AzureAd:TenantId`、`AzureAd:ClientId`，以及证书或客户端凭据。
禁止把真实凭据写入 appsettings.json 或提交到 Git。

## Conditional Access

- 全体内部人员至少要求 MFA。
- boss 和 super_admin 使用独立 Entra 安全组，并要求 phishing-resistant MFA。
- 推荐方法为 Passkey/FIDO2 安全密钥或 Windows Hello for Business。
- 禁止旧式认证；按条件要求合规设备和受信任位置。
- 保留两个受监控的紧急恢复账号，且不用于日常操作。

## 人员映射

登录后读取 Entra `oid`，将该值原样写入 `eggrack_auth_staff.staff_ref`。
不要用邮箱、UPN 或显示名称作为长期标识。

## 上线检查

- 未登录访问 `/security` 会跳转 Entra。
- 未映射的 Entra 用户登录后返回 403。
- 已映射但无权限的用户返回 403。
- super_admin 和 boss 的 Conditional Access 强认证策略已生效。
- Cookie 名称带 `__Host-`，并启用 Secure、HttpOnly、四小时固定过期。

