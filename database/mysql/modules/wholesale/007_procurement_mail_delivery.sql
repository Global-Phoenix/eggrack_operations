-- Delivery state for procurement mail tasks.
ALTER TABLE procurement_mail_tasks
  ADD COLUMN attempts TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER status,
  ADD COLUMN next_attempt_at INT UNSIGNED DEFAULT NULL AFTER attempts,
  ADD COLUMN locked_at INT UNSIGNED DEFAULT NULL AFTER next_attempt_at,
  ADD COLUMN last_error VARCHAR(1000) DEFAULT NULL AFTER locked_at,
  ADD COLUMN updated_at INT UNSIGNED NOT NULL DEFAULT 0 AFTER sent_at,
  ADD KEY idx_mail_dispatch(status,next_attempt_at,attempts,id);

UPDATE procurement_mail_tasks SET updated_at=created_at WHERE updated_at=0;
