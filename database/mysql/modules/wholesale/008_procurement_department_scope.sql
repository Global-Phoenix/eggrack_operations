-- Persist procurement ownership for department-scoped authorization.
ALTER TABLE purchase_plans
    ADD COLUMN department_id BIGINT UNSIGNED NULL AFTER assigned_buyer_id,
    ADD KEY idx_purchase_plans_department_status(department_id,status,updated_at),
    ADD CONSTRAINT fk_purchase_plans_department
      FOREIGN KEY(department_id) REFERENCES eggrack_auth_department(id)
      ON DELETE RESTRICT;

UPDATE purchase_plans p
SET p.department_id=(
 SELECT sd.department_id
 FROM eggrack_auth_staff_department sd
 WHERE sd.staff_id=p.assigned_buyer_id
 ORDER BY sd.is_primary DESC,sd.id
 LIMIT 1
)
WHERE p.department_id IS NULL;

-- Rebuild only the procurement grants owned by this migration. Unassigned
-- customer requests have no department owner, so plan creation remains global.
DELETE rp FROM eggrack_auth_role_permission rp
JOIN eggrack_auth_role r ON r.id=rp.role_id
JOIN eggrack_auth_permission p ON p.id=rp.permission_id
WHERE r.role_code IN ('department_admin','department_manager','department_staff')
  AND p.permission_code LIKE 'wholesale.%';

DELETE rs FROM eggrack_auth_role_scope rs
JOIN eggrack_auth_role r ON r.id=rs.role_id
JOIN eggrack_auth_permission p ON p.id=rs.permission_id
WHERE r.role_code IN ('department_admin','department_manager','department_staff')
  AND p.permission_code LIKE 'wholesale.%';

INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect)
SELECT r.id,p.id,'ALLOW'
FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON
 (r.role_code IN ('department_admin','department_manager') AND p.permission_code IN (
  'wholesale.purchase-plan.view','wholesale.purchase-plan.update',
  'wholesale.procurement.manage','wholesale.purchase-cost.manage',
  'wholesale.purchase-quote.approve'))
 OR
 (r.role_code='department_staff' AND p.permission_code IN (
  'wholesale.purchase-plan.view','wholesale.procurement.manage',
  'wholesale.purchase-cost.manage'))
WHERE r.role_code IN ('department_admin','department_manager','department_staff')
ON DUPLICATE KEY UPDATE effect=VALUES(effect);

INSERT INTO eggrack_auth_role_scope(role_id,permission_id,data_scope)
SELECT r.id,p.id,
 CASE WHEN r.role_code='department_staff' THEN 'SELF_OR_DEPARTMENT' ELSE 'DEPARTMENT' END
FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON
 (r.role_code IN ('department_admin','department_manager') AND p.permission_code IN (
  'wholesale.purchase-plan.view','wholesale.purchase-plan.update',
  'wholesale.procurement.manage','wholesale.purchase-cost.manage',
  'wholesale.purchase-quote.approve'))
 OR
 (r.role_code='department_staff' AND p.permission_code IN (
  'wholesale.purchase-plan.view','wholesale.procurement.manage',
  'wholesale.purchase-cost.manage'))
WHERE r.role_code IN ('department_admin','department_manager','department_staff')
ON DUPLICATE KEY UPDATE data_scope=VALUES(data_scope);
