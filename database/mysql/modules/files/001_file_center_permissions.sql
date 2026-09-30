-- Register the file-center menu/query permission after the security schema exists.
SET NAMES utf8mb4;

INSERT INTO eggrack_auth_permission
 (permission_code,permission_name,resource_type,description)
VALUES
 ('files.view','查看文件中心','API','查询当前采购数据范围内的客户附件和采购计划文件')
ON DUPLICATE KEY UPDATE permission_name=VALUES(permission_name),
 resource_type=VALUES(resource_type),description=VALUES(description),status=1;

-- Any role that can view purchase plans can query the corresponding file center.
INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect)
SELECT DISTINCT r.id,file_permission.id,'ALLOW'
FROM eggrack_auth_role r
JOIN eggrack_auth_role_permission purchase_grant ON purchase_grant.role_id=r.id AND purchase_grant.effect='ALLOW'
JOIN eggrack_auth_permission purchase_permission ON purchase_permission.id=purchase_grant.permission_id
  AND purchase_permission.permission_code='wholesale.purchase-plan.view'
JOIN eggrack_auth_permission file_permission ON file_permission.permission_code='files.view'
ON DUPLICATE KEY UPDATE effect=VALUES(effect);

INSERT INTO eggrack_auth_role_scope(role_id,permission_id,data_scope)
SELECT r.id,file_permission.id,
 COALESCE(purchase_scope.data_scope,r.default_scope)
FROM eggrack_auth_role r
JOIN eggrack_auth_role_permission file_grant ON file_grant.role_id=r.id AND file_grant.effect='ALLOW'
JOIN eggrack_auth_permission file_permission ON file_permission.id=file_grant.permission_id
  AND file_permission.permission_code='files.view'
LEFT JOIN eggrack_auth_permission purchase_permission
  ON purchase_permission.permission_code='wholesale.purchase-plan.view'
LEFT JOIN eggrack_auth_role_scope purchase_scope
  ON purchase_scope.role_id=r.id AND purchase_scope.permission_id=purchase_permission.id
ON DUPLICATE KEY UPDATE data_scope=VALUES(data_scope);

-- Invalidate cached authorization after adding the new grant.
UPDATE eggrack_auth_staff s
JOIN eggrack_auth_staff_role sr ON sr.staff_id=s.id
JOIN eggrack_auth_role_permission rp ON rp.role_id=sr.role_id AND rp.effect='ALLOW'
JOIN eggrack_auth_permission p ON p.id=rp.permission_id AND p.permission_code='files.view'
SET s.auth_version=s.auth_version+1;
