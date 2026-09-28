-- eggrack_operations permissions / MySQL 8.0+
-- New eggrack_auth_* tables only; no original table is changed or referenced.
SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS eggrack_auth_department (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 parent_id BIGINT UNSIGNED NULL,
 department_code VARCHAR(64) NOT NULL,
 department_name VARCHAR(128) NOT NULL,
 sort_order INT NOT NULL DEFAULT 0,
 status TINYINT UNSIGNED NOT NULL DEFAULT 1,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
 deleted_at DATETIME(3) NULL,
 UNIQUE KEY uk_auth_department_code(department_code),
 KEY idx_auth_department_parent(parent_id),
 CONSTRAINT fk_auth_department_parent FOREIGN KEY(parent_id)
  REFERENCES eggrack_auth_department(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_staff (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 staff_ref VARCHAR(128) NOT NULL COMMENT 'Original account ID, soft reference',
 staff_name VARCHAR(128) NOT NULL,
 mobile VARCHAR(32) NULL,
 email VARCHAR(190) NULL,
 status TINYINT UNSIGNED NOT NULL DEFAULT 1,
 auth_version BIGINT UNSIGNED NOT NULL DEFAULT 1,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
 deleted_at DATETIME(3) NULL,
 UNIQUE KEY uk_auth_staff_ref(staff_ref),
 KEY idx_auth_staff_status(status,deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_role (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 role_code VARCHAR(64) NOT NULL,
 role_name VARCHAR(128) NOT NULL,
 role_level SMALLINT UNSIGNED NOT NULL,
 default_scope VARCHAR(32) NOT NULL,
 is_system TINYINT UNSIGNED NOT NULL DEFAULT 0,
 status TINYINT UNSIGNED NOT NULL DEFAULT 1,
 description VARCHAR(500) NULL,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
 UNIQUE KEY uk_auth_role_code(role_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_permission (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 parent_id BIGINT UNSIGNED NULL,
 permission_code VARCHAR(128) NOT NULL,
 permission_name VARCHAR(128) NOT NULL,
 resource_type VARCHAR(32) NOT NULL DEFAULT 'API',
 resource_ref VARCHAR(255) NULL,
 status TINYINT UNSIGNED NOT NULL DEFAULT 1,
 description VARCHAR(500) NULL,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
 UNIQUE KEY uk_auth_permission_code(permission_code),
 KEY idx_auth_permission_parent(parent_id),
 CONSTRAINT fk_auth_permission_parent FOREIGN KEY(parent_id)
  REFERENCES eggrack_auth_permission(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_staff_department (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 staff_id BIGINT UNSIGNED NOT NULL,
 department_id BIGINT UNSIGNED NOT NULL,
 is_primary TINYINT UNSIGNED NOT NULL DEFAULT 0,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 UNIQUE KEY uk_auth_staff_department(staff_id,department_id),
 KEY idx_auth_sd_department(department_id,staff_id),
 FOREIGN KEY(staff_id) REFERENCES eggrack_auth_staff(id) ON DELETE CASCADE,
 FOREIGN KEY(department_id) REFERENCES eggrack_auth_department(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_staff_role (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 staff_id BIGINT UNSIGNED NOT NULL,
 role_id BIGINT UNSIGNED NOT NULL,
 department_id BIGINT UNSIGNED NULL COMMENT 'NULL global, otherwise department scoped',
 department_scope_id BIGINT UNSIGNED GENERATED ALWAYS AS
  (COALESCE(department_id,0)) STORED,
 valid_from DATETIME(3) NULL,
 valid_until DATETIME(3) NULL,
 granted_by BIGINT UNSIGNED NULL,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 UNIQUE KEY uk_auth_staff_role_scope(staff_id,role_id,department_scope_id),
 KEY idx_auth_sr_department(department_id,staff_id),
 FOREIGN KEY(staff_id) REFERENCES eggrack_auth_staff(id) ON DELETE CASCADE,
 FOREIGN KEY(role_id) REFERENCES eggrack_auth_role(id) ON DELETE RESTRICT,
 FOREIGN KEY(department_id) REFERENCES eggrack_auth_department(id) ON DELETE RESTRICT,
 FOREIGN KEY(granted_by) REFERENCES eggrack_auth_staff(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_role_permission (
 role_id BIGINT UNSIGNED NOT NULL,
 permission_id BIGINT UNSIGNED NOT NULL,
 effect VARCHAR(8) NOT NULL DEFAULT 'ALLOW',
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 PRIMARY KEY(role_id,permission_id),
 KEY idx_auth_rp_permission(permission_id,role_id),
 FOREIGN KEY(role_id) REFERENCES eggrack_auth_role(id) ON DELETE CASCADE,
 FOREIGN KEY(permission_id) REFERENCES eggrack_auth_permission(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_role_scope (
 role_id BIGINT UNSIGNED NOT NULL,
 permission_id BIGINT UNSIGNED NOT NULL,
 data_scope VARCHAR(32) NOT NULL,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
 PRIMARY KEY(role_id,permission_id),
 FOREIGN KEY(role_id) REFERENCES eggrack_auth_role(id) ON DELETE CASCADE,
 FOREIGN KEY(permission_id) REFERENCES eggrack_auth_permission(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS eggrack_auth_audit_log (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 operator_ref VARCHAR(128) NOT NULL,
 action_code VARCHAR(128) NOT NULL,
 target_type VARCHAR(64) NOT NULL,
 target_ref VARCHAR(128) NULL,
 before_data JSON NULL,
 after_data JSON NULL,
 request_id VARCHAR(128) NULL,
 ip_address VARCHAR(45) NULL,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 KEY idx_auth_audit_operator_time(operator_ref,created_at),
 KEY idx_auth_audit_target(target_type,target_ref,created_at),
 KEY idx_auth_audit_request(request_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO eggrack_auth_role
 (role_code,role_name,role_level,default_scope,is_system,status,description)
VALUES
 ('super_admin','超级管理员',10,'ALL',1,1,'全部系统和业务权限'),
 ('boss','老板',20,'ALL',1,1,'全部业务数据'),
 ('department_admin','部门管理员',30,'DEPARTMENT',1,1,'指定部门人员和配置'),
 ('department_manager','部门主管',40,'DEPARTMENT',1,1,'所负责部门'),
 ('department_staff','部门员工',50,'SELF_OR_DEPARTMENT',1,1,'自己或本部门，按权限决定')
ON DUPLICATE KEY UPDATE role_name=VALUES(role_name),role_level=VALUES(role_level),
 default_scope=VALUES(default_scope),is_system=VALUES(is_system),
 status=VALUES(status),description=VALUES(description);

INSERT INTO eggrack_auth_permission
 (permission_code,permission_name,resource_type)
VALUES
 ('auth.staff.read','查看人员','API'),('auth.staff.create','新增人员','API'),
 ('auth.staff.update','修改人员','API'),('auth.staff.disable','停用人员','ACTION'),
 ('auth.role.read','查看角色','API'),('auth.role.assign','授予角色','ACTION'),
 ('auth.role.revoke','撤销角色','ACTION'),('auth.department.read','查看部门','API'),
 ('auth.department.manage','管理部门','ACTION'),
 ('auth.permission.read','查看权限点','API'),
 ('auth.permission.manage','管理权限点','ACTION'),
 ('auth.audit.read','查看权限审计','API')
ON DUPLICATE KEY UPDATE permission_name=VALUES(permission_name),
 resource_type=VALUES(resource_type);

-- Super admin: all currently registered permissions.
INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect)
SELECT r.id,p.id,'ALLOW' FROM eggrack_auth_role r
CROSS JOIN eggrack_auth_permission p WHERE r.role_code='super_admin'
ON DUPLICATE KEY UPDATE effect=VALUES(effect);

-- Boss: permission configuration is read-only by default.
INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect)
SELECT r.id,p.id,'ALLOW' FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON p.permission_code IN
 ('auth.staff.read','auth.role.read','auth.department.read',
  'auth.permission.read','auth.audit.read')
WHERE r.role_code='boss'
ON DUPLICATE KEY UPDATE effect=VALUES(effect);

-- Department admin: staff and role operations limited to granted department.
INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect)
SELECT r.id,p.id,'ALLOW' FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON p.permission_code IN
 ('auth.staff.read','auth.staff.create','auth.staff.update','auth.staff.disable',
  'auth.role.read','auth.role.assign','auth.role.revoke','auth.department.read')
WHERE r.role_code='department_admin'
ON DUPLICATE KEY UPDATE effect=VALUES(effect);

INSERT INTO eggrack_auth_role_scope(role_id,permission_id,data_scope)
SELECT r.id,p.id,'DEPARTMENT' FROM eggrack_auth_role r
JOIN eggrack_auth_permission p ON p.permission_code IN
 ('auth.staff.read','auth.staff.create','auth.staff.update','auth.staff.disable',
  'auth.role.read','auth.role.assign','auth.role.revoke','auth.department.read')
WHERE r.role_code='department_admin'
ON DUPLICATE KEY UPDATE data_scope=VALUES(data_scope);



