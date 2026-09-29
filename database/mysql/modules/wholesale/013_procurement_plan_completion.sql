ALTER TABLE purchase_plans
    ADD COLUMN completed_at BIGINT UNSIGNED DEFAULT NULL AFTER target_completion_date;

UPDATE purchase_plans
SET planned_start_date=DATE(FROM_UNIXTIME(created_at))
WHERE planned_start_date IS NULL;

UPDATE purchase_plans
SET completed_at=updated_at
WHERE status=5 AND completed_at IS NULL;
