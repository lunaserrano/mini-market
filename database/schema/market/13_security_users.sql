-- ============================================================
-- 13_security_users.sql
-- Usuario de aplicación de mínimo privilegio: la Api se conecta con este usuario en vez de con una
-- cuenta administradora. Solo puede EJECUTAR los stored procedures del esquema market — no tiene
-- SELECT/INSERT/UPDATE/DELETE directo sobre ninguna tabla, así que aunque una consulta llegara sin
-- pasar por un procedure (o hubiera un intento de inyección SQL en la Api) no podría tocar los datos
-- más que a través de la superficie que exponen los procedures.
--
-- IMPORTANTE: cambia '<ELIGE-UNA-CONTRASEÑA-FUERTE>' antes de ejecutar este script. No reutilices
-- la contraseña de la cuenta administradora ni la subas a git: ponla en la connection string vía
-- variable de entorno / User Secrets / Key Vault, nunca en appsettings.*.json versionado.
--
-- Ejecuta este script conectado como una cuenta con permisos administrativos sobre la base de datos.
-- ============================================================

DECLARE @AppUser  sysname = N'market_app';
DECLARE @Password NVARCHAR(128) = N'<ELIGE-UNA-CONTRASEÑA-FUERTE>';

-- ---------------------------------------------------------------
-- Variante A — Azure SQL Database (o cualquier base con "contained database"): el usuario se crea
-- directamente en la base de datos, sin login a nivel de servidor.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @AppUser)
BEGIN
    DECLARE @sqlCreateUser NVARCHAR(MAX) =
        N'CREATE USER [' + @AppUser + N'] WITH PASSWORD = ''' + REPLACE(@Password, '''', '''''') + N''';';
    EXEC (@sqlCreateUser);
END
GO

/*
-- ---------------------------------------------------------------
-- Variante B — SQL Server on-prem/instancia clásica (login a nivel de servidor). Ejecuta este bloque
-- en vez del de arriba si NO estás en Azure SQL Database ni tienes "contained database" habilitado.
-- ---------------------------------------------------------------
USE master;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'market_app')
    CREATE LOGIN [market_app] WITH PASSWORD = N'<ELIGE-UNA-CONTRASEÑA-FUERTE>', CHECK_POLICY = ON;
GO
USE MiniMarket; -- ajusta al nombre real de tu base
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'market_app')
    CREATE USER [market_app] FOR LOGIN [market_app];
GO
*/

-- ---------------------------------------------------------------
-- Rol de aplicación: agrupa el permiso para poder auditarlo/otorgarlo a más de un usuario si hace falta.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'market_execute' AND type = 'R')
    CREATE ROLE market_execute AUTHORIZATION dbo;
GO

ALTER ROLE market_execute ADD MEMBER market_app;
GO

-- Necesario para que los procedures puedan crear/usar los table types de market.CodigoListType,
-- market.PermisoListType y market.EventoSeguridadListType como parámetros de entrada.
GRANT EXECUTE ON SCHEMA::market TO market_execute;

-- Sin GRANT SELECT/INSERT/UPDATE/DELETE sobre market.* : el usuario de aplicación no puede leer ni
-- escribir ninguna tabla directamente, solo a través de los procedures (que sí corren con los
-- permisos del propietario del esquema salvo que se cambie EXECUTE AS, comportamiento estándar de
-- "ownership chaining" de SQL Server: no requiere permisos extra en las tablas).
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::market TO market_execute;
GO
