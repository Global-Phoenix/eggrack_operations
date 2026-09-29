-- Fields required by the sourcing/inquiry/sample editing workflow.
ALTER TABLE procurement_inquiries
    ADD COLUMN currency CHAR(3) NOT NULL DEFAULT 'CNY' AFTER supplier_id,
    ADD COLUMN status VARCHAR(24) NOT NULL DEFAULT 'draft' AFTER terms,
    ADD COLUMN notes VARCHAR(1000) DEFAULT NULL AFTER status,
    ADD COLUMN updated_at INT UNSIGNED NOT NULL DEFAULT 0 AFTER created_at;

ALTER TABLE procurement_samples
    ADD COLUMN quantity DECIMAL(15,3) NOT NULL DEFAULT 1 AFTER supplier_id;