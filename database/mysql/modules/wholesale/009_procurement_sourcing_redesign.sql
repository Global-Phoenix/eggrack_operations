-- Structured supplier, quoted product details, sample attachments, and flexible plan costs.
ALTER TABLE procurement_suppliers
    ADD COLUMN address VARCHAR(500) DEFAULT NULL AFTER supplier_code,
    ADD COLUMN legal_representative VARCHAR(120) DEFAULT NULL AFTER address,
    ADD COLUMN contact_name VARCHAR(120) DEFAULT NULL AFTER legal_representative,
    ADD COLUMN contact_phone VARCHAR(80) DEFAULT NULL AFTER contact_name,
    ADD COLUMN website VARCHAR(500) DEFAULT NULL AFTER contact_phone;

UPDATE procurement_suppliers
SET contact_name=COALESCE(contact_name,NULLIF(JSON_UNQUOTE(JSON_EXTRACT(contact_json,'$.contact')),'')),
    contact_phone=COALESCE(contact_phone,NULLIF(JSON_UNQUOTE(JSON_EXTRACT(contact_json,'$.mobile')),''))
WHERE contact_json IS NOT NULL;

ALTER TABLE procurement_inquiries
    ADD COLUMN offered_product_name VARCHAR(255) DEFAULT NULL AFTER supplier_id,
    ADD COLUMN length_cm DECIMAL(12,3) DEFAULT NULL AFTER offered_product_name,
    ADD COLUMN width_cm DECIMAL(12,3) DEFAULT NULL AFTER length_cm,
    ADD COLUMN height_cm DECIMAL(12,3) DEFAULT NULL AFTER width_cm,
    ADD COLUMN weight_kg DECIMAL(12,3) DEFAULT NULL AFTER height_cm,
    ADD COLUMN color VARCHAR(120) DEFAULT NULL AFTER weight_kg,
    ADD COLUMN size_details VARCHAR(500) DEFAULT NULL AFTER color,
    ADD COLUMN parameter_details TEXT NULL AFTER size_details;

ALTER TABLE purchase_plan_files
    ADD COLUMN sample_id INT UNSIGNED DEFAULT NULL AFTER plan_id,
    ADD KEY idx_purchase_plan_files_sample(sample_id,status);

CREATE TABLE IF NOT EXISTS procurement_plan_cost_items (
    id INT UNSIGNED NOT NULL AUTO_INCREMENT,
    plan_id INT UNSIGNED NOT NULL,
    cost_name VARCHAR(120) NOT NULL,
    amount_cny DECIMAL(15,2) NOT NULL DEFAULT 0,
    sort_order INT UNSIGNED NOT NULL DEFAULT 0,
    created_by BIGINT UNSIGNED NOT NULL,
    updated_by BIGINT UNSIGNED NOT NULL,
    created_at INT UNSIGNED NOT NULL,
    updated_at INT UNSIGNED NOT NULL,
    PRIMARY KEY(id),
    KEY idx_procurement_plan_cost_items(plan_id,sort_order,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;
