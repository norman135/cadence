-- Runs once, when the database volume is first initialized.
-- Query statistics for performance work: SELECT * FROM pg_stat_statements ORDER BY total_exec_time DESC;
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
