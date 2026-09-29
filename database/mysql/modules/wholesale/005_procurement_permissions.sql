-- Register procurement permissions after the security module schema exists.
INSERT INTO eggrack_auth_permission
 (permission_code,permission_name,resource_type)
VALUES
 ('wholesale.purchase-plan.view','查看采购计划','API'),
 ('wholesale.purchase-plan.create','创建采购计划','ACTION'),
 ('wholesale.purchase-plan.update','更新采购计划','ACTION'),
 ('wholesale.procurement.manage','管理寻源询价和样品','ACTION'),
 ('wholesale.purchase-cost.manage','管理采购成本','ACTION'),
 ('wholesale.purchase-quote.approve','审批采购报价','ACTION')
ON DUPLICATE KEY UPDATE permission_name=VALUES(permission_name),
 resource_type=VALUES(resource_type),status=1;

-- Global roles retain ALL scope. Department roles are added by migration 008,
-- after purchase plan ownership and scoped queries are available.
INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect)
SELECT r.id,p.id,'ALLOW'
FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON p.permission_code IN (
 'wholesale.purchase-plan.view','wholesale.purchase-plan.create',
 'wholesale.purchase-plan.update','wholesale.procurement.manage',
 'wholesale.purchase-cost.manage','wholesale.purchase-quote.approve')
WHERE r.role_code IN ('super_admin','boss')
ON DUPLICATE KEY UPDATE effect=VALUES(effect);

INSERT INTO eggrack_auth_role_scope(role_id,permission_id,data_scope)
SELECT r.id,p.id,'ALL'
FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON p.permission_code IN (
 'wholesale.purchase-plan.view','wholesale.purchase-plan.create',
 'wholesale.purchase-plan.update','wholesale.procurement.manage',
 'wholesale.purchase-cost.manage','wholesale.purchase-quote.approve')
WHERE r.role_code IN ('super_admin','boss')
ON DUPLICATE KEY UPDATE data_scope=VALUES(data_scope);
