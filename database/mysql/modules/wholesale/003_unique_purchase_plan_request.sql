-- Enforce the business invariant: one purchase request has one procurement plan.
-- Verify duplicates before applying this migration.
SELECT request_id, COUNT(*) AS plan_count
FROM purchase_plans
GROUP BY request_id
HAVING COUNT(*) > 1;

ALTER TABLE purchase_plans
    ADD UNIQUE KEY uk_purchase_plans_request_id (request_id);