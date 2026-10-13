:setvar DatabaseName "MakanApp"
:setvar RuntimeLogin "makanapp_runtime"
:setvar RuntimeUser "makanapp_runtime"

/*
  Login باید قبلاً توسط DBA با secret خارج repository ساخته شده باشد.
  اجرا با migration/deployment principal انجام می‌شود، نه با runtime principal.
*/
USE [$(DatabaseName)];
GO

IF SUSER_ID(N'$(RuntimeLogin)') IS NULL
    THROW 51000, 'Runtime login must be created by DBA before this script.', 1;
GO

IF DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)') IS NULL
BEGIN
    DECLARE @createUser nvarchar(max) =
        N'CREATE USER ' + QUOTENAME(N'$(RuntimeUser)') +
        N' FOR LOGIN ' + QUOTENAME(N'$(RuntimeLogin)') + N';';
    EXEC sys.sp_executesql @createUser;
END;
GO

GRANT CONNECT TO [$(RuntimeUser)];
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[identity] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[organization] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[guardian] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[academic] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[assessment] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[storage] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[messaging] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[notification] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[commerce] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[reports] TO [$(RuntimeUser)];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[platform] TO [$(RuntimeUser)];
GO

/* ALTER و CONTROL عمداً grant نشده‌اند؛ DENY سراسری اضافه نمی‌شود تا DML صریح schemaها خنثی نشود. */
