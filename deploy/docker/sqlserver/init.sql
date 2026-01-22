-- SelfMX SQL Server Initialization Script
-- Run this script to create the database and application user
-- Usage: sqlcmd -S localhost -U sa -P 'YourPassword' -i init.sql

-- Create database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SelfMX')
BEGIN
    CREATE DATABASE SelfMX
    COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Database SelfMX created.';
END
ELSE
BEGIN
    PRINT 'Database SelfMX already exists.';
END
GO

USE SelfMX;
GO

-- Create application user (optional - for production deployments)
-- Uncomment and customize if you want a dedicated app user instead of SA
/*
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = 'selfmx_app')
BEGIN
    CREATE LOGIN selfmx_app WITH PASSWORD = 'AppPassword123!';
    PRINT 'Login selfmx_app created.';
END

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = 'selfmx_app')
BEGIN
    CREATE USER selfmx_app FOR LOGIN selfmx_app;
    ALTER ROLE db_owner ADD MEMBER selfmx_app;
    PRINT 'User selfmx_app created with db_owner role.';
END
*/

PRINT 'SelfMX database initialization complete.';
GO
