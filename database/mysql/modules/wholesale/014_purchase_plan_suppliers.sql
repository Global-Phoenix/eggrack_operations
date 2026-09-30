-- Explicit supplier membership for a purchase plan. Supplier master data remains company-wide.
CREATE TABLE IF NOT EXISTS procurement_plan_suppliers (
    plan_id INT UNSIGNED NOT NULL,
    supplier_id INT UNSIGNED NOT NULL,
    source_code VARCHAR(24) NOT NULL DEFAULT 'manual',
    created_by BIGINT UNSIGNED DEFAULT NULL,
    created_at INT UNSIGNED NOT NULL,
    PRIMARY KEY(plan_id,supplier_id),
    KEY idx_procurement_plan_suppliers_supplier(supplier_id,plan_id),
    CONSTRAINT fk_procurement_plan_suppliers_plan
      FOREIGN KEY(plan_id) REFERENCES purchase_plans(id) ON DELETE CASCADE,
    CONSTRAINT fk_procurement_plan_suppliers_supplier
      FOREIGN KEY(supplier_id) REFERENCES procurement_suppliers(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

INSERT IGNORE INTO procurement_plan_suppliers(plan_id,supplier_id,source_code,created_by,created_at)
SELECT i.plan_id,q.supplier_id,'inquiry',NULL,MIN(q.created_at)
FROM procurement_inquiries q
JOIN purchase_plan_items i ON i.id=q.plan_item_id
GROUP BY i.plan_id,q.supplier_id;

INSERT IGNORE INTO procurement_plan_suppliers(plan_id,supplier_id,source_code,created_by,created_at)
SELECT i.plan_id,s.supplier_id,'sample',NULL,MIN(s.created_at)
FROM procurement_samples s
JOIN purchase_plan_items i ON i.id=s.plan_item_id
WHERE s.supplier_id IS NOT NULL
GROUP BY i.plan_id,s.supplier_id;

INSERT IGNORE INTO procurement_plan_suppliers(plan_id,supplier_id,source_code,created_by,created_at)
SELECT i.plan_id,c.supplier_id,'candidate',NULL,MIN(c.created_at)
FROM procurement_candidate_products c
JOIN purchase_plan_items i ON i.id=c.plan_item_id
WHERE c.supplier_id IS NOT NULL
GROUP BY i.plan_id,c.supplier_id;
