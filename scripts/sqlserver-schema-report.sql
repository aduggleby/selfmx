-- Read-only schema report for a SelfMX SQL Server database.
-- Run before upgrading a pre-migration install to compare it with a freshly migrated schema:
--   sqlcmd -S <server> -U <user> -P <password> -C -d SelfMX -h -1 -W -i sqlserver-schema-report.sql > schema.txt
-- Index names and history tables differ by design and are excluded; compare columns and FKs.
SET NOCOUNT ON;
SELECT line FROM (
    SELECT 'col ' + TABLE_NAME + '.' + COLUMN_NAME + ' ' + DATA_TYPE
         + '(' + ISNULL(CAST(CHARACTER_MAXIMUM_LENGTH AS varchar(10)), '') + ') ' + IS_NULLABLE COLLATE DATABASE_DEFAULT AS line
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME NOT LIKE '__EFMigrationsHistory%'
    UNION ALL
    SELECT 'fk ' + fk.name COLLATE DATABASE_DEFAULT + ' ' + fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT
    FROM sys.foreign_keys fk
) report
ORDER BY line;
