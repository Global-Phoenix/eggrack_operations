-- Rename the legacy boss role without changing its identity or assignments.
-- Keeping the same role id preserves staff assignments, permissions and scopes.
SET NAMES utf8mb4;

UPDATE eggrack_auth_role legacy_role
LEFT JOIN eggrack_auth_role executive_role
  ON executive_role.role_code='executive'
SET legacy_role.role_code='executive',
    legacy_role.role_name='最高决策人',
    legacy_role.description='公司最高业务决策权限及全部业务数据'
WHERE legacy_role.role_code='boss'
  AND executive_role.id IS NULL;

UPDATE eggrack_auth_role
SET role_name='最高决策人',
    role_level=20,
    default_scope='ALL',
    is_system=1,
    status=1,
    description='公司最高业务决策权限及全部业务数据'
WHERE role_code='executive';
