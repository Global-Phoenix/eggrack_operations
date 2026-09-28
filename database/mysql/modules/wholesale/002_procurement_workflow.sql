-- Eggrack Operations procurement workflow extensions
-- MySQL 5.7; existing purchase_* and proforma_* tables are reused unchanged.
CREATE TABLE IF NOT EXISTS procurement_suppliers (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,supplier_name VARCHAR(255) NOT NULL,supplier_code VARCHAR(50) DEFAULT NULL,
 contact_json JSON DEFAULT NULL,status VARCHAR(20) NOT NULL DEFAULT 'active',created_at INT UNSIGNED NOT NULL,updated_at INT UNSIGNED NOT NULL,
 PRIMARY KEY(id),UNIQUE KEY uk_procurement_supplier_code(supplier_code),KEY idx_procurement_supplier_name(supplier_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS procurement_candidate_products (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,plan_item_id INT UNSIGNED NOT NULL,supplier_id INT UNSIGNED DEFAULT NULL,
 product_name VARCHAR(255) NOT NULL,reference_url VARCHAR(1000) DEFAULT NULL,specification_json JSON DEFAULT NULL,
 status VARCHAR(20) NOT NULL DEFAULT 'candidate',created_at INT UNSIGNED NOT NULL,updated_at INT UNSIGNED NOT NULL,
 PRIMARY KEY(id),KEY idx_candidate_plan_item(plan_item_id,status),KEY idx_candidate_supplier(supplier_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS procurement_inquiries (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,plan_item_id INT UNSIGNED NOT NULL,supplier_id INT UNSIGNED NOT NULL,
 unit_price_cny DECIMAL(15,4) DEFAULT NULL,moq DECIMAL(15,3) DEFAULT NULL,lead_days INT UNSIGNED DEFAULT NULL,
 valid_until DATE DEFAULT NULL,terms TEXT,created_at INT UNSIGNED NOT NULL,
 PRIMARY KEY(id),KEY idx_inquiry_plan_item(plan_item_id,created_at),KEY idx_inquiry_supplier(supplier_id,created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS procurement_samples (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,plan_item_id INT UNSIGNED NOT NULL,supplier_id INT UNSIGNED DEFAULT NULL,
 status VARCHAR(24) NOT NULL,cost_cny DECIMAL(15,2) NOT NULL DEFAULT 0,tracking_number VARCHAR(100) DEFAULT NULL,
 notes VARCHAR(1000) DEFAULT NULL,created_at INT UNSIGNED NOT NULL,updated_at INT UNSIGNED NOT NULL,
 PRIMARY KEY(id),KEY idx_sample_plan_item(plan_item_id,status),KEY idx_sample_supplier(supplier_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS procurement_quote_approvals (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,plan_id INT UNSIGNED NOT NULL,status VARCHAR(24) NOT NULL,
 quote_usd DECIMAL(15,2) NOT NULL,profit_rate DECIMAL(8,4) NOT NULL,submitted_by INT UNSIGNED NOT NULL,
 decided_by INT UNSIGNED DEFAULT NULL,decision_note VARCHAR(1000) DEFAULT NULL,submitted_at INT UNSIGNED NOT NULL,decided_at INT UNSIGNED DEFAULT NULL,
 PRIMARY KEY(id),KEY idx_approval_plan(plan_id,submitted_at),KEY idx_approval_status(status,submitted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS procurement_mail_tasks (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,plan_id INT UNSIGNED NOT NULL,approval_id INT UNSIGNED NOT NULL,
 template_code VARCHAR(120) NOT NULL,recipient VARCHAR(320) NOT NULL,status VARCHAR(24) NOT NULL DEFAULT 'pending',
 payload_json JSON NOT NULL,created_at INT UNSIGNED NOT NULL,sent_at INT UNSIGNED DEFAULT NULL,
 PRIMARY KEY(id),KEY idx_mail_plan(plan_id,created_at),KEY idx_mail_status(status,created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;
