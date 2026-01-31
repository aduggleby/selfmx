---
title: External SQL Server Setup
description: Configure SelfMX to use an existing SQL Server instance instead of the containerized one.
toc: true
---

## Overview

By default, SelfMX includes a containerized SQL Server 2022 instance. You can instead connect to an existing SQL Server for environments where you already have database infrastructure.

## Create Database and User

Connect to your SQL Server using any SQL client (Azure Data Studio, SSMS, sqlcmd, etc.):

```bash
# Example using sqlcmd
sqlcmd -S YOUR_SERVER_IP,1433 -U sa -P 'YOUR_SA_PASSWORD' -C
```

Run these SQL commands to create the database and a dedicated login:

```sql
-- Create database
CREATE DATABASE SelfMX;
GO

-- Create login with strong password
CREATE LOGIN selfmx WITH PASSWORD = 'YourSecurePassword123!';
GO

-- Create user and grant permissions
USE SelfMX;
GO

CREATE USER selfmx FOR LOGIN selfmx;
GO

ALTER ROLE db_owner ADD MEMBER selfmx;
GO
```

Type `exit` to quit sqlcmd.

## Connection String

Update your connection string to point to your SQL Server:

```bash
ConnectionStrings__DefaultConnection=Server=your-server,1433;Database=SelfMX;User Id=selfmx;Password=YourSecurePassword123!;TrustServerCertificate=True
```

### Connection String Parameters

| Parameter | Description |
|-----------|-------------|
| `Server` | Hostname or IP and port (e.g., `192.168.1.100,1433`) |
| `Database` | Database name (`SelfMX`) |
| `User Id` | SQL login name |
| `Password` | SQL login password |
| `TrustServerCertificate` | Set to `True` for self-signed certs |
| `Encrypt` | Set to `True` for encrypted connections |
| `Max Pool Size` | Connection pool maximum (default: 100) |
| `Min Pool Size` | Connection pool minimum (default: 0) |

### TrueNAS Connection String

For TrueNAS SCALE installations with additional pooling:

```bash
ConnectionStrings__DefaultConnection=Server=YOUR_TRUENAS_IP,1433;Database=SelfMX;User Id=selfmx;Password=YourSecurePassword123!;TrustServerCertificate=true;Encrypt=True;MultipleActiveResultSets=true;Max Pool Size=200;Min Pool Size=20
```

## Security Considerations

- Use a dedicated SQL login for SelfMX (not `sa`)
- Use a strong, unique password
- Restrict network access to the SQL Server port (1433)
- Consider using encrypted connections (`Encrypt=True`)

## Troubleshooting

### Connection Failed

1. Verify the server IP and port are correct
2. Test connectivity: `nc -zv YOUR_SERVER_IP 1433`
3. Check firewall rules allow port 1433
4. Verify the login credentials are correct

### Permission Errors

Ensure the `selfmx` user has `db_owner` role on the `SelfMX` database:

```sql
USE SelfMX;
SELECT dp.name, dp.type_desc, p.permission_name
FROM sys.database_principals dp
LEFT JOIN sys.database_permissions p ON dp.principal_id = p.grantee_principal_id
WHERE dp.name = 'selfmx';
```
