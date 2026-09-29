-- Independent internal procurement-plan profile. Customer requests remain reference data only.
ALTER TABLE purchase_plans
    ADD COLUMN plan_title VARCHAR(200) DEFAULT NULL AFTER plan_number,
    ADD COLUMN priority_code VARCHAR(20) NOT NULL DEFAULT 'normal' AFTER status,
    ADD COLUMN planned_start_date DATE DEFAULT NULL AFTER priority_code,
    ADD COLUMN target_completion_date DATE DEFAULT NULL AFTER planned_start_date,
    ADD KEY idx_purchase_plans_schedule(priority_code,target_completion_date,status);

UPDATE purchase_plans
SET plan_title=plan_number
WHERE plan_title IS NULL OR plan_title='';
