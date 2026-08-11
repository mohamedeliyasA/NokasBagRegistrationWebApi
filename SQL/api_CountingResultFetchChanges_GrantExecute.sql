USE [Nattsafe]
GO
/****** Grants EXECUTE on the new proc to whichever database principal already has EXECUTE on
        api_CancelBagIfRegistered (i.e. the same login the Function's NattsafeSQLDBConnString
        already uses) - avoids hardcoding/guessing a login name. Run after
        api_CountingResultFetchChanges.sql. Safe to re-run (checks before granting).
 ******/
DECLARE @Principal SYSNAME;

SELECT TOP (1) @Principal = dp.name
FROM sys.database_permissions perm
JOIN sys.database_principals dp ON dp.principal_id = perm.grantee_principal_id
WHERE perm.major_id = OBJECT_ID('dbo.api_CancelBagIfRegistered')
  AND perm.permission_name = 'EXECUTE'
  AND perm.state = 'G';

IF @Principal IS NULL
BEGIN
    RAISERROR('Could not find an existing EXECUTE grant on dbo.api_CancelBagIfRegistered to copy - grant EXECUTE on dbo.api_CountingResultFetchChanges manually to the correct login.', 16, 1);
    RETURN;
END

DECLARE @Sql NVARCHAR(MAX) = N'GRANT EXECUTE ON [dbo].[api_CountingResultFetchChanges] TO ' + QUOTENAME(@Principal) + N';';
PRINT @Sql;
EXEC sp_executesql @Sql;
GO
